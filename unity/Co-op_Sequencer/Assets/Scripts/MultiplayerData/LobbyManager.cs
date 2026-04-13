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
    }

    void OnDisable()
    {
        if (_server == null) return;
        _server.OnClientConnected    -= HandleClientConnected;
        _server.OnClientDisconnected -= HandleClientDisconnected;
        InputReceiver.OnButtonInput  -= RouteButton;
        InputReceiver.OnScratchInput -= RouteScratch;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Returns the PlayerInputReceiver for the given clientId, or null.</summary>
    public PlayerInputReceiver GetPlayerInput(string clientId)
    {
        _receivers.TryGetValue(clientId, out var r);
        return r;
    }

    /// <summary>
    /// Sends each player's symbol assignments to their phone.
    /// Call this after GameManager.AssignSymbolsToPlayers.
    /// </summary>
    public void SendSymbolAssignments()
    {
        // Resolve GameManager at runtime — the serialized reference may be null
        // after a scene transition since LobbyManager persists via DontDestroyOnLoad.
        if (gameManager == null)
            gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogError("[LobbyManager] No GameManager found — cannot send symbol images.");
            return;
        }

        foreach (var player in Lobby.players)
        {
            var b1Img = gameManager.SpriteToBase64(player.button1Symbol);
            var b2Img = gameManager.SpriteToBase64(player.button2Symbol);

            Debug.Log($"[LobbyManager] Player {player.id}: b1Image={(b1Img != null ? $"{b1Img.Length} chars" : "NULL")}, b2Image={(b2Img != null ? $"{b2Img.Length} chars" : "NULL")}");

            // Send button1 image
            var msg1 = JsonUtility.ToJson(new PlayerAssignedMessage
            {
                playerId      = player.id,
                color         = player.color,
                button1Symbol = player.button1Symbol.ToString(),
                button1Image  = b1Img,
                button2Symbol = "",
                button2Image  = "",
            });
            _server.SendToClient(player.clientId, msg1);
            Debug.Log($"[LobbyManager] Sent button1 to player {player.id}: {player.button1Symbol} ({msg1.Length} chars)");

            // Send button2 image
            var msg2 = JsonUtility.ToJson(new PlayerAssignedMessage
            {
                playerId      = player.id,
                color         = player.color,
                button1Symbol = "",
                button1Image  = "",
                button2Symbol = player.button2Symbol.ToString(),
                button2Image  = b2Img,
            });
            _server.SendToClient(player.clientId, msg2);
            Debug.Log($"[LobbyManager] Sent button2 to player {player.id}: {player.button2Symbol} ({msg2.Length} chars)");
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

                var msg = JsonUtility.ToJson(new PlayerAssignedMessage { playerId = disconnected.id, color = disconnected.color });
                _server.SendToClient(clientId, msg);

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

        var player   = Lobby.AddPlayer(clientId);
        var receiver = new PlayerInputReceiver(player);
        _receivers[clientId] = receiver;

        var msg2 = JsonUtility.ToJson(new PlayerAssignedMessage { playerId = player.id, color = player.color });
        _server.SendToClient(clientId, msg2);

        Debug.Log($"[LobbyManager] Player {player.id} ({player.color}) joined ({clientId})");
        OnPlayerJoined?.Invoke(receiver);
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
        Lobby.RemovePlayer(clientId);

        Debug.Log($"[LobbyManager] Player {player.id} left ({clientId})");
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
}
