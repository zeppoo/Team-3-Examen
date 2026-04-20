using System;
using System.Collections.Generic;
using UnityEngine;
using CoopSequencer.Networking;

/// <summary>
/// Manages the lobby lifecycle and owns one PlayerInputReceiver per connected player.
/// Attach to the same GameObject as WebSocketServer and InputReceiver.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Lobby Settings")]
    public string lobbyName  = "game";
    public int    maxPlayers = 4;

    [Header("Dependencies")]
    [SerializeField] private GameManager gameManager;

    public Lobby Lobby { get; private set; }

    /// <summary>Unique ID for this lobby instance — set by LobbyDisplay at boot.</summary>
    public string LobbyId { get; set; } = "";

    // Reconnect tokens: reconnectToken → Player (survives brief disconnects pre-game too).
    private readonly Dictionary<string, Player> _tokensToPlayer = new();
    private readonly Dictionary<int, string>    _playerToToken = new();

    // Clients we haven't yet committed to either "new join" or "rejoin".
    // We wait a short window (REJOIN_GRACE_MS) for a rejoin handshake before
    // falling through to AddPlayer — otherwise a fresh placeholder's token
    // rotation would wipe the very token the phone is trying to redeem.
    private readonly HashSet<string> _pendingClients = new();
    private const float REJOIN_GRACE_SECONDS = 0.75f;

    /// <summary>Fired on the main thread when a player joins.</summary>
    public event Action<PlayerInputReceiver> OnPlayerJoined;

    /// <summary>Fired on the main thread when a player leaves.</summary>
    public event Action<Player> OnPlayerLeft;

    /// <summary>Fired when a player disconnects during a game.</summary>
    public event Action<Player> OnPlayerDisconnected;

    /// <summary>Fired when a player reconnects during a game.</summary>
    public event Action<Player> OnPlayerReconnected;

    private readonly Dictionary<string, PlayerInputReceiver> _receivers = new();
    private WebSocketServer _server;

    void Awake()
    {
        // Persist across scenes and prevent duplicates
        if (FindObjectsByType<LobbyManager>(FindObjectsSortMode.None).Length > 1)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);

        Lobby   = new Lobby(lobbyName, maxPlayers);
        _server = GetComponent<WebSocketServer>();
        _server.CanClientConnect = () => GameStarted || !Lobby.IsFull;
    }

    void OnEnable()
    {
        _server.OnClientConnected    += HandleClientConnected;
        _server.OnClientDisconnected += HandleClientDisconnected;
        InputReceiver.OnButtonInput  += RouteButton;
        InputReceiver.OnScratchInput += RouteScratch;
        InputReceiver.OnSliderInput  += RouteSlider;
        InputReceiver.OnSymbolSelect += HandleSymbolSelect;
        InputReceiver.OnRejoin       += HandleRejoin;
    }

    void OnDisable()
    {
        if (_server == null) return;
        _server.OnClientConnected    -= HandleClientConnected;
        _server.OnClientDisconnected -= HandleClientDisconnected;
        InputReceiver.OnButtonInput  -= RouteButton;
        InputReceiver.OnScratchInput -= RouteScratch;
        InputReceiver.OnSliderInput  -= RouteSlider;
        InputReceiver.OnSymbolSelect -= HandleSymbolSelect;
        InputReceiver.OnRejoin       -= HandleRejoin;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Returns the PlayerInputReceiver for the given clientId, or null.</summary>
    public PlayerInputReceiver GetPlayerInput(string clientId)
    {
        _receivers.TryGetValue(clientId, out var r);
        return r;
    }

    /// <summary>
    /// Sends each player's assignment info to their phone.
    /// Call this after GameManager.AssignSymbolsToPlayers.
    /// </summary>
    public void SendSymbolAssignments()
    {
        foreach (var player in Lobby.players)
        {
            _playerToToken.TryGetValue(player.id, out var token);
            var msg = JsonUtility.ToJson(new PlayerAssignedMessage
            {
                playerId       = player.id,
                color          = player.color,
                symbol         = player.hasSymbol ? player.symbol.ToString() : "",
                name           = player.displayName,
                lobbyId        = LobbyId,
                reconnectToken = token ?? "",
            });
            _server.SendToClient(player.clientId, msg);
            Debug.Log($"[LobbyManager] Sent player assignment to player {player.id} ({player.color})");
        }
    }

    /// <summary>
    /// Sends a score update to a specific player's phone.
    /// </summary>
    public void SendScoreUpdate(int playerId, int totalScore, int lastHitPoints, string rating)
    {
        var player = Lobby.GetPlayerById(playerId);
        if (player == null || !player.connected) return;

        var msg = JsonUtility.ToJson(new ScoreUpdateMessage
        {
            playerId      = playerId,
            score         = totalScore,
            lastHitPoints = lastHitPoints,
            rating        = rating,
        });
        _server.SendToClient(player.clientId, msg);
    }

    /// <summary>
    /// Sends a lane update to a specific player's phone.
    /// </summary>
    public void SendLaneUpdate(int playerId, int lane, int totalLanes)
    {
        var player = Lobby.GetPlayerById(playerId);
        if (player == null || !player.connected) return;

        var msg = JsonUtility.ToJson(new LaneUpdateMessage
        {
            playerId   = playerId,
            lane       = lane,
            totalLanes = totalLanes,
        });
        _server.SendToClient(player.clientId, msg);
    }

    /// <summary>True once the first round has started — reconnecting clients
    /// are mapped back to their existing Player instead of creating a new one.</summary>
    public bool GameStarted { get; set; }

    /// <summary>True when all lobby players are connected.</summary>
    public bool AllPlayersConnected => Lobby.players.TrueForAll(p => p.connected);

    // ── Connection handling ───────────────────────────────────────────────

    private void HandleClientConnected(string clientId)
    {
        // During a game, try to reconnect to a disconnected player slot
        if (GameStarted)
        {
            var disconnected = Lobby.GetFirstDisconnected();
            if (disconnected != null)
            {
                var oldClientId = disconnected.clientId;
                disconnected.clientId  = clientId;
                disconnected.connected = true;

                // Move the receiver to the new clientId
                if (_receivers.TryGetValue(oldClientId, out var recv))
                {
                    _receivers.Remove(oldClientId);
                    _receivers[clientId] = recv;
                }

                var reconnectToken = IssueReconnectToken(disconnected);
                var msg = JsonUtility.ToJson(new PlayerAssignedMessage
                {
                    playerId       = disconnected.id,
                    color          = disconnected.color,
                    symbol         = disconnected.hasSymbol ? disconnected.symbol.ToString() : "",
                    name           = disconnected.displayName,
                    lobbyId        = LobbyId,
                    reconnectToken = reconnectToken,
                });
                _server.SendToClient(clientId, msg);
                SendSymbolsUpdate(clientId);

                Debug.Log($"[LobbyManager] Player {disconnected.id} reconnected ({oldClientId} → {clientId})");
                OnPlayerReconnected?.Invoke(disconnected);
                return;
            }

            // Game in progress but no disconnected slot — reject
            Debug.LogWarning($"[LobbyManager] Rejecting client {clientId} — game in progress, no open slot.");
            _server.SendToClient(clientId, "{\"type\":\"error\",\"reason\":\"game_in_progress\"}");
            _server.DisconnectClient(clientId);
            return;
        }

        // Pre-game: give the client a short window to send a rejoin handshake
        // before we allocate a fresh slot. This prevents IssueReconnectToken from
        // wiping a token the phone is about to redeem.
        _pendingClients.Add(clientId);
        StartCoroutine(FinalizeJoinAfterGrace(clientId));
        Debug.Log($"[LobbyManager] Client {clientId} connected — waiting {REJOIN_GRACE_SECONDS}s for rejoin");
    }

    private System.Collections.IEnumerator FinalizeJoinAfterGrace(string clientId)
    {
        yield return new WaitForSeconds(REJOIN_GRACE_SECONDS);
        if (!_pendingClients.Contains(clientId)) yield break; // already handled by rejoin
        _pendingClients.Remove(clientId);

        if (Lobby.IsFull)
        {
            Debug.LogWarning($"[LobbyManager] Rejecting client {clientId} — lobby full after grace window.");
            _server.SendToClient(clientId, "{\"type\":\"error\",\"reason\":\"lobby_full\"}");
            _server.DisconnectClient(clientId);
            yield break;
        }

        var player   = Lobby.AddPlayer(clientId);
        var receiver = new PlayerInputReceiver(player);
        _receivers[clientId] = receiver;

        var token = IssueReconnectToken(player);
        var msg = JsonUtility.ToJson(new PlayerAssignedMessage
        {
            playerId       = player.id,
            color          = player.color,
            lobbyId        = LobbyId,
            reconnectToken = token,
        });
        _server.SendToClient(clientId, msg);
        SendSymbolsUpdate(clientId);

        Debug.Log($"[LobbyManager] Player {player.id} ({player.color}) joined ({clientId})");
        OnPlayerJoined?.Invoke(receiver);
    }

    private string IssueReconnectToken(Player player)
    {
        // Replace any old token for this player
        if (_playerToToken.TryGetValue(player.id, out var old))
            _tokensToPlayer.Remove(old);

        var token = System.Guid.NewGuid().ToString("N");
        _tokensToPlayer[token]     = player;
        _playerToToken[player.id]  = token;
        return token;
    }

    // ── Reconnect handshake ───────────────────────────────────────────────

    private void HandleRejoin(string clientId, CoopSequencer.Networking.RejoinMessage msg)
    {
        // Always consume any pending-client entry so the grace-window coroutine
        // won't later allocate a fresh slot for this clientId.
        bool wasPending = _pendingClients.Remove(clientId);

        if (string.IsNullOrEmpty(msg.lobbyId) || msg.lobbyId != LobbyId)
        {
            Debug.Log($"[LobbyManager] rejoin rejected — lobbyId mismatch (got {msg.lobbyId}, have {LobbyId})");
            SendRejoinRejected(clientId, "lobby_mismatch");
            if (wasPending) FallbackFreshJoin(clientId);
            return;
        }

        if (string.IsNullOrEmpty(msg.reconnectToken) ||
            !_tokensToPlayer.TryGetValue(msg.reconnectToken, out var existing))
        {
            Debug.Log($"[LobbyManager] rejoin rejected — unknown token ({msg.reconnectToken})");
            SendRejoinRejected(clientId, "unknown_token");
            if (wasPending) FallbackFreshJoin(clientId);
            return;
        }

        // Re-bind the existing player to the new clientId.
        var oldClientId = existing.clientId;
        existing.clientId  = clientId;
        existing.connected = true;

        if (_receivers.TryGetValue(oldClientId, out var oldRecv))
        {
            _receivers.Remove(oldClientId);
            _receivers[clientId] = oldRecv;
        }
        else
        {
            _receivers[clientId] = new PlayerInputReceiver(existing);
        }

        // Make sure the player is in the lobby list (it will be, unless it was removed pre-game).
        if (!Lobby.players.Contains(existing)) Lobby.players.Add(existing);

        var token = IssueReconnectToken(existing); // rotate token on every rejoin
        var assigned = JsonUtility.ToJson(new PlayerAssignedMessage
        {
            playerId       = existing.id,
            color          = existing.color,
            symbol         = existing.hasSymbol ? existing.symbol.ToString() : "",
            name           = existing.displayName,
            lobbyId        = LobbyId,
            reconnectToken = token,
        });
        _server.SendToClient(clientId, assigned);
        SendSymbolsUpdate(clientId);

        Debug.Log($"[LobbyManager] Player {existing.id} rejoined via token ({oldClientId} → {clientId})");
        if (GameStarted) OnPlayerReconnected?.Invoke(existing);
        else             BroadcastSymbolsUpdate();
    }

    private void FallbackFreshJoin(string clientId)
    {
        // Allocate a fresh slot for a client whose rejoin was rejected.
        if (Lobby.IsFull)
        {
            Debug.LogWarning($"[LobbyManager] Rejecting client {clientId} — lobby full.");
            _server.SendToClient(clientId, "{\"type\":\"error\",\"reason\":\"lobby_full\"}");
            _server.DisconnectClient(clientId);
            return;
        }

        var player   = Lobby.AddPlayer(clientId);
        var receiver = new PlayerInputReceiver(player);
        _receivers[clientId] = receiver;

        var token = IssueReconnectToken(player);
        var msg = JsonUtility.ToJson(new PlayerAssignedMessage
        {
            playerId       = player.id,
            color          = player.color,
            lobbyId        = LobbyId,
            reconnectToken = token,
        });
        _server.SendToClient(clientId, msg);
        SendSymbolsUpdate(clientId);

        Debug.Log($"[LobbyManager] Player {player.id} ({player.color}) joined after failed rejoin ({clientId})");
        OnPlayerJoined?.Invoke(receiver);
    }

    private void SendRejoinRejected(string clientId, string reason)
    {
        // Reuse the error envelope the client already understands.
        _server.SendToClient(clientId, $"{{\"type\":\"error\",\"reason\":\"rejoin_{reason}\"}}");
    }

    // ── Symbol selection ──────────────────────────────────────────────────

    private void HandleSymbolSelect(string clientId, CoopSequencer.Networking.SymbolSelectMessage msg)
    {
        var player = Lobby.GetPlayer(clientId);
        if (player == null)
        {
            Debug.LogWarning($"[LobbyManager] symbol_select from unknown client {clientId}");
            return;
        }

        if (!System.Enum.TryParse<SymbolType>(msg.symbol, out var chosen) ||
            System.Array.IndexOf(Lobby.SelectableSymbols, chosen) < 0)
        {
            SendSymbolRejected(clientId, msg.symbol, "invalid");
            return;
        }

        if (Lobby.IsSymbolTaken(chosen) && !(player.hasSymbol && player.symbol == chosen))
        {
            SendSymbolRejected(clientId, msg.symbol, "taken");
            return;
        }

        player.symbol      = chosen;
        player.hasSymbol   = true;
        player.displayName = string.IsNullOrEmpty(msg.name) ? player.displayName : msg.name;

        var assigned = JsonUtility.ToJson(new PlayerAssignedMessage
        {
            playerId = player.id,
            color    = player.color,
            symbol   = chosen.ToString(),
            name     = player.displayName,
        });
        _server.SendToClient(clientId, assigned);

        Debug.Log($"[LobbyManager] Player {player.id} claimed {chosen} as \"{player.displayName}\"");
        BroadcastSymbolsUpdate();
    }

    private void SendSymbolRejected(string clientId, string symbol, string reason)
    {
        var msg = JsonUtility.ToJson(new CoopSequencer.Networking.SymbolRejectedMessage
        {
            symbol = symbol,
            reason = reason,
        });
        _server.SendToClient(clientId, msg);
    }

    private CoopSequencer.Networking.SymbolsUpdateMessage BuildSymbolsUpdate()
    {
        var available = Lobby.GetAvailableSymbols();
        var taken     = Lobby.GetTakenSymbols();
        var msg = new CoopSequencer.Networking.SymbolsUpdateMessage
        {
            available = new string[available.Count],
            taken     = new string[taken.Count],
        };
        for (int i = 0; i < available.Count; i++) msg.available[i] = available[i].ToString();
        for (int i = 0; i < taken.Count; i++)     msg.taken[i]     = taken[i].ToString();
        return msg;
    }

    private void SendSymbolsUpdate(string clientId)
    {
        var json = JsonUtility.ToJson(BuildSymbolsUpdate());
        _server.SendToClient(clientId, json);
    }

    private void BroadcastSymbolsUpdate()
    {
        var json = JsonUtility.ToJson(BuildSymbolsUpdate());
        foreach (var p in Lobby.players)
        {
            if (p.connected) _server.SendToClient(p.clientId, json);
        }
    }

    private void HandleClientDisconnected(string clientId)
    {
        if (!_receivers.TryGetValue(clientId, out var receiver)) return;

        if (GameStarted)
        {
            // Keep the player in the lobby — just mark as disconnected
            receiver.player.connected = false;
            Debug.Log($"[LobbyManager] Player {receiver.player.id} disconnected (kept in lobby)");
            OnPlayerDisconnected?.Invoke(receiver.player);
            return;
        }

        _receivers.Remove(clientId);
        var player = receiver.player;
        bool freedSymbol = player.hasSymbol;
        Lobby.RemovePlayer(clientId);

        Debug.Log($"[LobbyManager] Player {player.id} left ({clientId})");
        if (freedSymbol) BroadcastSymbolsUpdate();
        OnPlayerLeft?.Invoke(player);
    }

    // ── Input routing ─────────────────────────────────────────────────────

    private PlayerInputReceiver ResolveReceiver(string playerField)
    {
        // Try direct clientId lookup first
        if (_receivers.TryGetValue(playerField, out var receiver))
            return receiver;

        // Try as playerId (int) — find the player, then look up by their clientId
        if (int.TryParse(playerField, out int id))
        {
            var player = Lobby.GetPlayerById(id);
            if (player != null && _receivers.TryGetValue(player.clientId, out var recv))
                return recv;
        }

        return null;
    }

    private void RouteButton(ButtonInputEvent e)
    {
        var receiver = ResolveReceiver(e.player);
        if (receiver != null)
            receiver.DispatchButton(e);
    }

    private void RouteScratch(ScratchInputEvent e)
    {
        var receiver = ResolveReceiver(e.player);
        if (receiver != null)
            receiver.DispatchScratch(e);
    }

    private void RouteSlider(SliderInputEvent e)
    {
        var receiver = ResolveReceiver(e.player);
        if (receiver != null)
            receiver.DispatchSlider(e);
    }
}
