using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to a Canvas GameObject together with WebSocketServer.
/// Shows the WebSocket connection QR code so players can scan it.
///
/// Required scene setup:
///   - GameObject with this component AND WebSocketServer
///   - A UnityEngine.UI.RawImage  → assign to qrImage
///   - (Optional) TextMeshProUGUI → assign to statusText
///   - (Optional) TextMeshProUGUI → assign to ipPortText
/// </summary>
public class LobbyDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RawImage UI element that will show the QR code.")]
    public RawImage qrImage;

    [Tooltip("(Optional) Text showing connection status.")]
    public TMP_Text statusText;

    [Tooltip("(Optional) Text showing IP:Port.")]
    public TMP_Text ipPortText;

    [Header("Lobby Settings")]
    [Tooltip("Human-readable lobby name embedded in the QR payload.")]
    public string lobbyName = "game";

    [Tooltip("Port advertised in the QR code. Set to 8443 when running the wss-proxy, or match WebSocketServer.port for direct connections.")]
    public int advertisedPort = 8443;

    [Header("QR Visual")]
    [Tooltip("Pixels per QR module (higher = bigger image).")]
    [Range(4, 32)]
    public int pixelsPerModule = 16;

    [Tooltip("Save the QR code as a PNG next to the executable for debugging.")]
    public bool saveQRToDisk = true;

    private WebSocketServer _server;

    void Awake()
    {
        _server = GetComponent<WebSocketServer>();
        if (_server == null)
            _server = FindFirstObjectByType<WebSocketServer>();

        if (_server == null)
        {
            Debug.LogError("[LobbyDisplay] No WebSocketServer found in scene!");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        _server.OnClientConnected    += OnClientConnected;
        _server.OnClientDisconnected += OnClientDisconnected;

        RefreshQRCode();
    }

    void OnDestroy()
    {
        if (_server == null) return;
        _server.OnClientConnected    -= OnClientConnected;
        _server.OnClientDisconnected -= OnClientDisconnected;
    }

    // ── QR generation ────────────────────────────────────────────────────

    private void RefreshQRCode()
    {
        string ip   = _server.LocalIP;
        int    port = advertisedPort > 0 ? advertisedPort : _server.port;

        // Payload expected by the mobile app
        string payload = BuildPayload(ip, port, lobbyName);

        if (ipPortText != null)
            ipPortText.text = $"ws://{ip}:{port}  |  lobby: {lobbyName}";

        if (statusText != null)
            statusText.text = "Waiting for players...";

        try
        {
            Texture2D tex = QRCodeGenerator.Generate(payload, pixelsPerModule);
            if (qrImage != null)
            {
                qrImage.texture = tex;
                qrImage.SetNativeSize();
            }
            Debug.Log($"[LobbyDisplay] QR payload: {payload}");

            if (saveQRToDisk)
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "lobby_qr.png");
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[LobbyDisplay] QR saved to: {path}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LobbyDisplay] QR generation failed: {ex.Message}");
        }
    }

    private static string BuildPayload(string ip, int port, string lobby)
    {
        // Matches what the mobile app's QR scanner expects:
        // { "ip": "...", "port": ..., "lobby": "..." }
        // We build it manually to avoid requiring Newtonsoft in Unity
        return $"{{\"ip\":\"{ip}\",\"port\":{port},\"lobby\":\"{EscapeJson(lobby)}\"}}";
    }

    private static string EscapeJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── Connection callbacks ──────────────────────────────────────────────

    private void OnClientConnected(string clientId)
    {
        if (statusText != null)
            statusText.text = $"Players connected: {_server.ConnectedClientCount}";
        Debug.Log($"[LobbyDisplay] Player joined: {clientId}");
    }

    private void OnClientDisconnected(string clientId)
    {
        if (statusText != null)
            statusText.text = _server.ConnectedClientCount > 0
                ? $"Players connected: {_server.ConnectedClientCount}"
                : "Waiting for players...";
    }
}
