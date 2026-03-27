using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Spawns a cloudflared quick-tunnel pointing at the local WebSocket server.
/// Once the tunnel URL is ready, fires OnTunnelReady with the hostname.
///
/// Add this component to the same GameObject as LobbyDisplay + WebSocketServer.
/// </summary>
public class CloudflaredTunnel : MonoBehaviour
{
    [Tooltip("Port of the local WebSocket server to tunnel.")]
    public int localPort = 8080;

    [Tooltip("Path to the cloudflared binary. Leave empty to use PATH.")]
    public string cloudflaredPath = "cloudflared";

    /// <summary>Fired on the main thread when the tunnel hostname is known.</summary>
    public event Action<string> OnTunnelReady;

    private Process _process;
    private CancellationTokenSource _cts;

    void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => StartTunnel(_cts.Token));
    }

    void OnDestroy() => KillTunnel();
    void OnApplicationQuit() => KillTunnel();

    private void StartTunnel(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = cloudflaredPath,
                Arguments              = $"tunnel --url http://localhost:{localPort} --no-autoupdate --protocol http2",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            _process = new Process { StartInfo = psi };
            _process.Start();

            UnityEngine.Debug.Log("[CloudflaredTunnel] Process started, waiting for tunnel URL...");

            // cloudflared prints the tunnel URL to stderr
            ReadUntilUrl(_process.StandardError, ct);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[CloudflaredTunnel] Failed to start: {ex.Message}");
        }
    }

    private void ReadUntilUrl(System.IO.StreamReader reader, CancellationToken ct)
    {
        // Matches lines like: https://xxxx-xxxx-xxxx.trycloudflare.com
        var urlRegex = new Regex(@"https://([a-z0-9\-]+\.trycloudflare\.com)", RegexOptions.IgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            string line;
            try   { line = reader.ReadLine(); }
            catch { break; }

            if (line == null) break;

            UnityEngine.Debug.Log($"[cloudflared] {line}");

            var match = urlRegex.Match(line);
            if (match.Success)
            {
                string hostname = match.Groups[1].Value;
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Tunnel ready: {hostname}");

                // Marshal back to main thread
                var captured = hostname;
                MainThreadDispatcher.Enqueue(() => OnTunnelReady?.Invoke(captured));
                break;
            }
        }
    }

    private void KillTunnel()
    {
        _cts?.Cancel();
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
                _process = null;
                UnityEngine.Debug.Log("[CloudflaredTunnel] Tunnel process killed.");
            }
        }
        catch { }
    }
}
