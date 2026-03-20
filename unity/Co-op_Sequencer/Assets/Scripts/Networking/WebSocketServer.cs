using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Hosts a WebSocket server inside Unity.
/// Attach this to a persistent GameObject. Other scripts can subscribe to
/// OnMessageReceived or call BroadcastMessage() / SendToClient().
/// </summary>
public class WebSocketServer : MonoBehaviour
{
    [Header("Server Settings")]
    [Tooltip("Port the WebSocket server will listen on.")]
    public int port = 8080;

    [Tooltip("Optional secret token. If non-empty the first message from each client must be this token or the connection is dropped.")]
    public string accessToken = "";

    // ── Public state (read from other scripts) ────────────────────────────
    public string LocalIP { get; private set; } = "";
    public bool IsRunning { get; private set; } = false;
    public int ConnectedClientCount => _clients.Count;

    // ── Events (raised on the main thread via the queue below) ───────────
    public event Action<string, string> OnMessageReceived; // (clientId, json)
    public event Action<string>         OnClientConnected;    // clientId
    public event Action<string>         OnClientDisconnected; // clientId

    // ── Internals ─────────────────────────────────────────────────────────
    private HttpListener _httpListener;
    private CancellationTokenSource _cts;
    private readonly Dictionary<string, WebSocket> _clients = new();
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        LocalIP = GetLocalIP();
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartServer();
    }

    void Update()
    {
        // Drain the main-thread queue so events fire on the game thread
        while (_mainThreadQueue.TryDequeue(out var action))
            action?.Invoke();
    }

    void OnDestroy() => StopServer();
    void OnApplicationQuit() => StopServer();

    // ─────────────────────────────────────────────────────────────────────
    public void StartServer()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://*:{port}/");

        try
        {
            _httpListener.Start();
        }
        catch (HttpListenerException ex)
        {
            Debug.LogError($"[WebSocketServer] Failed to start on port {port}: {ex.Message}");
            return;
        }

        IsRunning = true;
        Debug.Log($"[WebSocketServer] Listening on ws://{LocalIP}:{port}");

        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void StopServer()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();

        foreach (var kv in _clients)
        {
            try { kv.Value.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait(500); }
            catch { /* ignore */ }
        }
        _clients.Clear();

        try { _httpListener?.Stop(); } catch { /* ignore */ }
        Debug.Log("[WebSocketServer] Stopped.");
    }

    // ─────────────────────────────────────────────────────────────────────
    /// Send a raw string to all connected clients.
    public new void BroadcastMessage(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var kv in _clients)
        {
            SendBytes(kv.Key, kv.Value, bytes);
        }
    }

    /// Send a raw string to one specific client.
    public void SendToClient(string clientId, string json)
    {
        if (_clients.TryGetValue(clientId, out var ws))
            SendBytes(clientId, ws, Encoding.UTF8.GetBytes(json));
    }

    // ─────────────────────────────────────────────────────────────────────
    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _httpListener.GetContextAsync();
            }
            catch
            {
                break;
            }

            if (ctx.Request.IsWebSocketRequest)
                _ = HandleClientAsync(ctx, ct);
            else
            {
                ctx.Response.StatusCode = 426; // Upgrade Required
                ctx.Response.Close();
            }
        }
    }

    private async Task HandleClientAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        WebSocketContext wsCtx;
        try
        {
            wsCtx = await ctx.AcceptWebSocketAsync(null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketServer] WebSocket handshake failed: {ex.Message}");
            ctx.Response.Close();
            return;
        }

        var ws = wsCtx.WebSocket;
        var clientId = Guid.NewGuid().ToString("N")[..8];

        // ── Optional token authentication ─────────────────────────────
        if (!string.IsNullOrEmpty(accessToken))
        {
            var buf = new byte[256];
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
            var token = Encoding.UTF8.GetString(buf, 0, result.Count).Trim();
            if (token != accessToken)
            {
                Debug.LogWarning($"[WebSocketServer] Client {clientId} sent wrong token, disconnecting.");
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Bad token", ct);
                return;
            }
        }

        _clients[clientId] = ws;
        _mainThreadQueue.Enqueue(() => OnClientConnected?.Invoke(clientId));
        Debug.Log($"[WebSocketServer] Client connected: {clientId}  (total: {_clients.Count})");

        var buffer = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var id  = clientId; // capture for closure
                    _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(id, msg));
                }
            }
        }
        catch (OperationCanceledException) { /* server stopping */ }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketServer] Client {clientId} error: {ex.Message}");
        }
        finally
        {
            _clients.Remove(clientId);
            _mainThreadQueue.Enqueue(() => OnClientDisconnected?.Invoke(clientId));
            Debug.Log($"[WebSocketServer] Client disconnected: {clientId}  (total: {_clients.Count})");

            try { ws.Dispose(); } catch { /* ignore */ }
        }
    }

    private static void SendBytes(string clientId, WebSocket ws, byte[] bytes)
    {
        if (ws.State != WebSocketState.Open) return;
        try
        {
            ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketServer] Send to {clientId} failed: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    public static string GetLocalIP()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
