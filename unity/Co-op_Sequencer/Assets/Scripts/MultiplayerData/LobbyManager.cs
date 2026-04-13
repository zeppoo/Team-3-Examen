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
        _server.CanClientConnect = () => !Lobby.IsFull;
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

            var msg = JsonUtility.ToJson(new PlayerAssignedMessage
            {
                playerId      = player.id,
                color         = player.color,
                button1Symbol = player.button1Symbol.ToString(),
                button1Image  = b1Img,
                button2Symbol = player.button2Symbol.ToString(),
                button2Image  = b2Img,
            });
            _server.SendToClient(player.clientId, msg);
            Debug.Log($"[LobbyManager] Sent symbols to player {player.id}: {player.button1Symbol} / {player.button2Symbol}");
        }
    }

    // ── Connection handling ───────────────────────────────────────────────

    private void HandleClientConnected(string clientId)
    {
        var player   = Lobby.AddPlayer(clientId);
        var receiver = new PlayerInputReceiver(player);
        _receivers[clientId] = receiver;

        var msg = JsonUtility.ToJson(new PlayerAssignedMessage { playerId = player.id, color = player.color });
        _server.SendToClient(clientId, msg);

        Debug.Log($"[LobbyManager] Player {player.id} ({player.color}) joined ({clientId})");
        OnPlayerJoined?.Invoke(receiver);
    }

    private void HandleClientDisconnected(string clientId)
    {
        if (!_receivers.TryGetValue(clientId, out var receiver)) return;

        _receivers.Remove(clientId);
        var player = receiver.player;
        Lobby.RemovePlayer(clientId);

        Debug.Log($"[LobbyManager] Player {player.id} left ({clientId})");
        OnPlayerLeft?.Invoke(player);
    }

    // ── Input routing ─────────────────────────────────────────────────────

    private void RouteButton(ButtonInputEvent e)
    {
        if (_receivers.TryGetValue(e.player, out var receiver))
            receiver.DispatchButton(e);
        else
            Debug.LogWarning($"[LobbyManager] Button input from unknown player {e.player}");
    }

    private void RouteScratch(ScratchInputEvent e)
    {
        if (_receivers.TryGetValue(e.player, out var receiver))
            receiver.DispatchScratch(e);
        else
            Debug.LogWarning($"[LobbyManager] Scratch input from unknown player {e.player}");
    }
}
