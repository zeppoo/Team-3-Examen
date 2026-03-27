using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Spawns cloudflared pointing at the local WebSocket server.
///
/// Quick tunnel (default): no setup needed, hostname changes every run.
/// Named tunnel: stable hostname, requires a Cloudflare tunnel ID + credentials file.
///   1. cloudflared tunnel create <name>
///   2. cloudflared tunnel route dns <tunnel-id> <your-hostname>
///   3. Set TunnelId to the tunnel UUID and Hostname to your configured DNS name.
///
/// Add this component to the same GameObject as LobbyDisplay + WebSocketServer.
///
/// Tunnel health: after the tunnel becomes ready, a background task polls the
/// HTTPS endpoint every <see cref="pollIntervalSeconds"/> seconds. On failure it
/// kills the process and spawns a new one, firing OnTunnelReady again with the
/// new hostname so LobbyDisplay can refresh the QR code.
/// </summary>
public class CloudflaredTunnel : MonoBehaviour
{
    [Tooltip("Port of the local WebSocket server to tunnel.")]
    public int localPort = 8080;

    [Tooltip("Path to the cloudflared binary. Leave empty to use PATH.")]
    public string cloudflaredPath = "cloudflared";

    [Header("Named Tunnel (optional — leave empty to use a quick tunnel)")]
    [Tooltip("Tunnel UUID from 'cloudflared tunnel list'. Leave empty for a quick tunnel.")]
    public string tunnelId = "";

    [Tooltip("The stable hostname routed to this tunnel (e.g. game.example.com).")]
    public string hostname = "";

    [Header("Health Check")]
    [Tooltip("Seconds between tunnel availability polls. 0 = disabled.")]
    public float pollIntervalSeconds = 15f;

    [Tooltip("How many consecutive failures before the tunnel is restarted.")]
    public int failuresBeforeRestart = 2;

    /// <summary>Fired on the main thread when the tunnel hostname is known.</summary>
    public event Action<string> OnTunnelReady;

    private Process _process;
    private CancellationTokenSource _cts;
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => TunnelLoop(_cts.Token));
    }

    void OnDestroy() => KillTunnel();
    void OnApplicationQuit() => KillTunnel();

    private bool IsNamedTunnel => !string.IsNullOrWhiteSpace(tunnelId);

    // ── Main loop ─────────────────────────────────────────────────────────

    private async Task TunnelLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string currentHost = StartProcess(ct);
            if (currentHost == null)
            {
                // Process failed to start — wait before retrying
                await Task.Delay(5000, ct).ContinueWith(_ => { });
                continue;
            }

            MainThreadDispatcher.Enqueue(() => OnTunnelReady?.Invoke(currentHost));

            if (pollIntervalSeconds <= 0 || IsNamedTunnel)
            {
                // No polling — just drain stderr until cancelled
                DrainStderr(_process, ct);
                break;
            }

            // Poll until the tunnel dies or we detect failure
            await PollUntilDead(currentHost, ct);

            if (ct.IsCancellationRequested) break;

            UnityEngine.Debug.LogWarning("[CloudflaredTunnel] Tunnel unreachable — restarting...");
            KillProcess();
        }
    }

    // ── Process management ────────────────────────────────────────────────

    /// <summary>Starts cloudflared, blocks until the tunnel URL is confirmed, returns the hostname.</summary>
    private string StartProcess(CancellationToken ct)
    {
        try
        {
            string args = IsNamedTunnel
                ? $"tunnel --no-autoupdate --protocol http2 run {tunnelId}"
                : $"tunnel --url http://localhost:{localPort} --no-autoupdate --protocol http2";

            var psi = new ProcessStartInfo
            {
                FileName               = cloudflaredPath,
                Arguments              = args,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            _process = new Process { StartInfo = psi };
            _process.Start();

            UnityEngine.Debug.Log($"[CloudflaredTunnel] Process started ({(IsNamedTunnel ? $"named tunnel {tunnelId}" : "quick tunnel")})");

            if (IsNamedTunnel)
            {
                return hostname;
            }
            else
            {
                return ReadUntilUrl(_process.StandardError, ct);
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[CloudflaredTunnel] Failed to start: {ex.Message}");
            return null;
        }
    }

    private void DrainStderr(Process process, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var line = process.StandardError.ReadLine();
                if (line == null) break;
                UnityEngine.Debug.Log($"[cloudflared] {line}");
            }
        }
        catch { }
    }

    private void KillProcess()
    {
        try
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(2000);
            }
            _process?.Dispose();
            _process = null;
        }
        catch { }
    }

    private void KillTunnel()
    {
        _cts?.Cancel();
        KillProcess();
        UnityEngine.Debug.Log("[CloudflaredTunnel] Tunnel stopped.");
    }

    // ── Stderr parsing ────────────────────────────────────────────────────

    private string ReadUntilUrl(System.IO.StreamReader reader, CancellationToken ct)
    {
        var urlRegex   = new Regex(@"https://([a-z0-9\-]+\.trycloudflare\.com)", RegexOptions.IgnoreCase);
        var readyRegex = new Regex(@"Registered tunnel connection", RegexOptions.IgnoreCase);

        string pendingHost = null;

        while (!ct.IsCancellationRequested)
        {
            string line;
            try   { line = reader.ReadLine(); }
            catch { break; }
            if (line == null) break;

            UnityEngine.Debug.Log($"[cloudflared] {line}");

            if (pendingHost == null)
            {
                var match = urlRegex.Match(line);
                if (match.Success)
                    pendingHost = match.Groups[1].Value;
            }

            if (pendingHost != null && readyRegex.IsMatch(line))
            {
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Tunnel ready: {pendingHost}");
                // Keep draining stderr on a background thread so the process isn't blocked
                var reader2 = reader;
                var ct2     = ct;
                Task.Run(() => DrainStderr(_process, ct2));
                return pendingHost;
            }
        }

        return null;
    }

    // ── Health polling ────────────────────────────────────────────────────

    private async Task PollUntilDead(string host, CancellationToken ct)
    {
        int failures = 0;
        var url      = $"https://{host}";
        int intervalMs = (int)(pollIntervalSeconds * 1000);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(intervalMs, ct).ContinueWith(_ => { });
            if (ct.IsCancellationRequested) return;

            bool alive = await PingTunnel(url);

            if (alive)
            {
                failures = 0;
            }
            else
            {
                failures++;
                UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Health check failed ({failures}/{failuresBeforeRestart}) — {host}");
                if (failures >= failuresBeforeRestart) return;
            }

            // Also stop polling if the process has already exited
            if (_process == null || _process.HasExited)
            {
                UnityEngine.Debug.LogWarning("[CloudflaredTunnel] Process exited unexpectedly.");
                return;
            }
        }
    }

    private static async Task<bool> PingTunnel(string url)
    {
        try
        {
            var response = await _http.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, url),
                HttpCompletionOption.ResponseHeadersRead
            );
            // Any HTTP response (even 4xx/5xx) means the tunnel is routing traffic
            UnityEngine.Debug.Log($"[CloudflaredTunnel] Health check OK — {(int)response.StatusCode}");
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Health check error: {ex.Message}");
            return false;
        }
    }
}
