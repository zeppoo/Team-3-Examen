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
        Task.Run(() => StartTunnelOnce(_cts.Token));
    }

    void OnDestroy() => KillTunnel();
    void OnApplicationQuit() => KillTunnel();

    /// <summary>Kills the current tunnel and starts a fresh one.</summary>
    public void RestartTunnel()
    {
        KillTunnel();
        _cts = new CancellationTokenSource();
        Task.Run(() => StartTunnelOnce(_cts.Token));
    }

    private bool IsNamedTunnel => !string.IsNullOrWhiteSpace(tunnelId);

    // ── Main logic ────────────────────────────────────────────────────────

    private async Task StartTunnelOnce(CancellationToken ct)
    {
        string currentHost = StartProcess(ct);
        if (currentHost == null)
        {
            // StartProcess already logs the specific error (rate limit, etc.)
            return;
        }

        MainThreadDispatcher.Enqueue(() => OnTunnelReady?.Invoke(currentHost));

        if (pollIntervalSeconds <= 0 || IsNamedTunnel)
        {
            DrainStderr(_process, ct);
            return;
        }

        await PollUntilDead(currentHost, ct);

        if (!ct.IsCancellationRequested)
        {
            UnityEngine.Debug.LogWarning("[CloudflaredTunnel] Tunnel unreachable. Use the restart button to try again.");
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
        var rateLimitRegex = new Regex(@"429 Too Many Requests|status_code=""429", RegexOptions.IgnoreCase);

        string pendingHost = null;

        while (!ct.IsCancellationRequested)
        {
            string line;
            try   { line = reader.ReadLine(); }
            catch { break; }
            if (line == null) break;

            UnityEngine.Debug.Log($"[cloudflared] {line}");

            // Detect Cloudflare rate limiting (429 = exceeded 200 concurrent in-flight requests)
            if (rateLimitRegex.IsMatch(line))
            {
                UnityEngine.Debug.LogError("[CloudflaredTunnel] Rate limited by Cloudflare (429). Quick tunnels support max 200 concurrent in-flight requests. Use the restart button to try again.");
                KillProcess();
                return null;
            }

            if (pendingHost == null)
            {
                var match = urlRegex.Match(line);
                if (match.Success)
                    pendingHost = match.Groups[1].Value;
            }

            if (pendingHost != null && readyRegex.IsMatch(line))
            {
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Tunnel ready: {pendingHost}");
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
