using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the lobby lifecycle and owns one PlayerInputReceiver per connected player.
/// Attach to the same GameObject as WebSocketServer and InputReceiver.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Lobby Settings")]
    public string lobbyName  = "game";
    public int    maxPlayers = 4;

    public Lobby Lobby { get; private set; }

    /// <summary>Fired on the main thread when a player joins.</summary>
    public event Action<PlayerInputReceiver> OnPlayerJoined;

    /// <summary>Fired on the main thread when a player leaves.</summary>
    public event Action<Player> OnPlayerLeft;

    private readonly Dictionary<string, PlayerInputReceiver> _receivers = new();
    private WebSocketServer _server;

    void Awake()
    {
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

    // ── Connection handling ───────────────────────────────────────────────

    private void HandleClientConnected(string clientId)
    {
        var player   = Lobby.AddPlayer(clientId);
        var receiver = new PlayerInputReceiver(player);
        _receivers[clientId] = receiver;

        Debug.Log($"[LobbyManager] Player {player.id} joined ({clientId})");
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
