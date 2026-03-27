using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Raw TCP WebSocket server — works on all platforms including Unity/Linux.
/// Avoids HttpListener which misreports IsWebSocketRequest on Mono.
/// </summary>
public class WebSocketServer : MonoBehaviour
{
    [Header("Server Settings")]
    public int port = 8080;

    [Tooltip("Optional. If set, the first message from each client must match or they are dropped.")]
    public string accessToken = "";

    public string LocalIP     { get; private set; } = "";
    public bool   IsRunning   { get; private set; } = false;
    public int    ConnectedClientCount => _clients.Count;

    public event Action<string, string> OnMessageReceived;
    public event Action<string>         OnClientConnected;
    public event Action<string>         OnClientDisconnected;

    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private readonly Dictionary<string, TcpWsClient> _clients = new();
    private readonly ConcurrentQueue<Action> _mainThreadQueue  = new();

    void Awake()
    {
        LocalIP = GetLocalIP();
        DontDestroyOnLoad(gameObject);
    }

    void Start()  => StartServer();
    void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke();
    }
    void OnDestroy()         => StopServer();
    void OnApplicationQuit() => StopServer();

    // ── Public API ────────────────────────────────────────────────────────

    public void StartServer()
    {
        if (IsRunning) return;
        _cts      = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        IsRunning = true;
        Debug.Log($"[WebSocketServer] Listening on ws://{LocalIP}:{port}");
        Task.Run(() => AcceptLoop(_cts.Token));
    }

    public void StopServer()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
        foreach (var kv in _clients)
            try { kv.Value.Close(); } catch { }
        _clients.Clear();
        Debug.Log("[WebSocketServer] Stopped.");
    }

    public new void BroadcastMessage(string json)
    {
        foreach (var kv in _clients)
            kv.Value.Send(json);
    }

    public void SendToClient(string clientId, string json)
    {
        if (_clients.TryGetValue(clientId, out var c)) c.Send(json);
    }

    // ── Accept loop ───────────────────────────────────────────────────────

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try   { tcp = await _listener.AcceptTcpClientAsync(); }
            catch { break; }
            _ = HandleClientAsync(tcp, ct);
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        tcp.NoDelay = true;
        var stream = tcp.GetStream();
        var clientId = Guid.NewGuid().ToString("N")[..8];

        // ── WebSocket handshake ───────────────────────────────────────────
        try
        {
            if (!await DoHandshake(stream, ct))
            {
                tcp.Close();
                return;
            }
        }
        catch
        {
            tcp.Close();
            return;
        }

        // ── Optional token auth ───────────────────────────────────────────
        var wsClient = new TcpWsClient(tcp, stream);

        if (!string.IsNullOrEmpty(accessToken))
        {
            var msg = await wsClient.ReceiveAsync(ct);
            if (msg == null || msg.Trim() != accessToken)
            {
                wsClient.Close();
                return;
            }
        }

        _clients[clientId] = wsClient;
        _mainThreadQueue.Enqueue(() => OnClientConnected?.Invoke(clientId));
        Debug.Log($"[WebSocketServer] Client connected: {clientId} (total: {_clients.Count})");

        // ── Receive loop ──────────────────────────────────────────────────
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var msg = await wsClient.ReceiveAsync(ct);
                if (msg == null) break; // connection closed

                var id = clientId;
                _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(id, msg));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocketServer] Client {clientId}: {ex.Message}");
        }
        finally
        {
            _clients.Remove(clientId);
            _mainThreadQueue.Enqueue(() => OnClientDisconnected?.Invoke(clientId));
            Debug.Log($"[WebSocketServer] Client disconnected: {clientId} (total: {_clients.Count})");
            wsClient.Close();
        }
    }

    // ── WebSocket handshake ───────────────────────────────────────────────

    private static async Task<bool> DoHandshake(NetworkStream stream, CancellationToken ct)
    {
        // Read HTTP request headers
        var sb  = new StringBuilder();
        var buf = new byte[4096];
        while (true)
        {
            int n = await stream.ReadAsync(buf, 0, buf.Length, ct);
            if (n == 0) return false;
            sb.Append(Encoding.UTF8.GetString(buf, 0, n));
            if (sb.ToString().Contains("\r\n\r\n")) break;
        }

        var request = sb.ToString();
        var keyMatch = Regex.Match(request, @"Sec-WebSocket-Key:\s*(.+)\r\n", RegexOptions.IgnoreCase);
        if (!keyMatch.Success) return false;

        var key      = keyMatch.Groups[1].Value.Trim();
        var accept   = ComputeAccept(key);
        var response = "HTTP/1.1 101 Switching Protocols\r\n"
                     + "Upgrade: websocket\r\n"
                     + "Connection: Upgrade\r\n"
                     + $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

        var respBytes = Encoding.UTF8.GetBytes(response);
        await stream.WriteAsync(respBytes, 0, respBytes.Length, ct);
        return true;
    }

    private static string ComputeAccept(string key)
    {
        const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        var hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(key + magic));
        return Convert.ToBase64String(hash);
    }

    // ── IP helper ─────────────────────────────────────────────────────────

    public static string GetLocalIP()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            s.Connect("8.8.8.8", 65530);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    // ── TcpWsClient: framing ──────────────────────────────────────────────

    private class TcpWsClient
    {
        private readonly TcpClient     _tcp;
        private readonly NetworkStream _stream;

        public TcpWsClient(TcpClient tcp, NetworkStream stream)
        {
            _tcp    = tcp;
            _stream = stream;
        }

        public void Close()
        {
            try { _tcp.Close(); } catch { }
        }

        /// <summary>Send a UTF-8 text frame.</summary>
        public void Send(string text)
        {
            try
            {
                var payload = Encoding.UTF8.GetBytes(text);
                var frame   = BuildFrame(payload);
                _stream.Write(frame, 0, frame.Length);
            }
            catch { }
        }

        /// <summary>Read one WebSocket frame and return its text payload, or null on close.</summary>
        public async Task<string> ReceiveAsync(CancellationToken ct)
        {
            // Read first 2 header bytes
            var header = new byte[2];
            if (!await ReadExact(_stream, header, 2, ct)) return null;

            bool masked      = (header[1] & 0x80) != 0;
            int  payloadLen  = header[1] & 0x7F;
            byte opcode      = (byte)(header[0] & 0x0F);

            // Close frame
            if (opcode == 0x8) return null;

            // Extended payload length
            if (payloadLen == 126)
            {
                var ext = new byte[2];
                if (!await ReadExact(_stream, ext, 2, ct)) return null;
                payloadLen = (ext[0] << 8) | ext[1];
            }
            else if (payloadLen == 127)
            {
                var ext = new byte[8];
                if (!await ReadExact(_stream, ext, 8, ct)) return null;
                // Only handle reasonable sizes
                payloadLen = (int)((long)ext[4] << 24 | (long)ext[5] << 16 | (long)ext[6] << 8 | ext[7]);
            }

            // Masking key
            byte[] mask = null;
            if (masked)
            {
                mask = new byte[4];
                if (!await ReadExact(_stream, mask, 4, ct)) return null;
            }

            // Payload
            var data = new byte[payloadLen];
            if (!await ReadExact(_stream, data, payloadLen, ct)) return null;

            if (masked)
                for (int i = 0; i < data.Length; i++)
                    data[i] ^= mask[i % 4];

            return Encoding.UTF8.GetString(data);
        }

        private static byte[] BuildFrame(byte[] payload)
        {
            int len = payload.Length;
            byte[] frame;

            if (len <= 125)
            {
                frame    = new byte[2 + len];
                frame[1] = (byte)len;
            }
            else if (len <= 65535)
            {
                frame    = new byte[4 + len];
                frame[1] = 126;
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)(len & 0xFF);
            }
            else
            {
                frame    = new byte[10 + len];
                frame[1] = 127;
                for (int i = 0; i < 8; i++)
                    frame[9 - i] = (byte)(len >> (8 * i));
            }

            frame[0] = 0x81; // FIN + text opcode
            Buffer.BlockCopy(payload, 0, frame, frame.Length - len, len);
            return frame;
        }

        private static async Task<bool> ReadExact(Stream s, byte[] buf, int count, CancellationToken ct)
        {
            int read = 0;
            while (read < count)
            {
                int n = await s.ReadAsync(buf, read, count - read, ct);
                if (n == 0) return false;
                read += n;
            }
            return true;
        }
    }
}
