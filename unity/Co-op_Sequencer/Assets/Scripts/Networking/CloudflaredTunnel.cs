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
/// Named tunnel (preferred): stable hostname via a system-service tunnel.
///   1. cloudflared tunnel create <name>
///   2. cloudflared tunnel route dns <tunnel-id> <your-hostname>
///   3. Install as service: sudo cloudflared service install
///   4. Set Hostname to your configured DNS name (e.g. game.example.com).
///
/// Quick tunnel (fallback): no setup needed, hostname changes every run.
///   Used automatically when no named tunnel hostname is set, or when
///   the named tunnel is unreachable.
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

    [Header("Named Tunnel (stable hostname via system service)")]
    [Tooltip("The stable hostname routed to your named tunnel (e.g. game.example.com). Leave empty to always use quick tunnels.")]
    public string hostname = "";

    [Tooltip("Seconds to wait for the named tunnel to respond before falling back to a quick tunnel.")]
    public float namedTunnelTimeoutSeconds = 5f;

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
        Task.Run(() => StartTunnel(_cts.Token));
    }

    void OnDestroy()
    {
        if (_process != null) KillTunnel();
    }
    void OnApplicationQuit() => KillTunnel();

    /// <summary>Kills the current tunnel and starts a fresh one.</summary>
    public void RestartTunnel()
    {
        KillTunnel();
        _cts = new CancellationTokenSource();
        Task.Run(() => StartTunnel(_cts.Token));
    }

    private bool HasNamedTunnel => !string.IsNullOrWhiteSpace(hostname);

    // ── Main logic ────────────────────────────────────────────────────────

    private async Task StartTunnel(CancellationToken ct)
    {
        // Try named tunnel first (running as system service — no process to start)
        if (HasNamedTunnel)
        {
            UnityEngine.Debug.Log($"[CloudflaredTunnel] Checking named tunnel at {hostname}...");
            bool reachable = await CheckNamedTunnel(hostname, ct);

            if (reachable)
            {
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Named tunnel is live: {hostname}");
                MainThreadDispatcher.Enqueue(() => OnTunnelReady?.Invoke(hostname));

                // Poll to detect if named tunnel goes down
                await PollNamedTunnel(hostname, ct);

                if (!ct.IsCancellationRequested)
                {
                    UnityEngine.Debug.LogWarning("[CloudflaredTunnel] Named tunnel went down. Falling back to quick tunnel...");
                    await StartQuickTunnel(ct);
                }
                return;
            }

            UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Named tunnel at {hostname} is not reachable. Falling back to quick tunnel...");
        }

        await StartQuickTunnel(ct);
    }

    private async Task StartQuickTunnel(CancellationToken ct)
    {
        string currentHost = StartQuickTunnelProcess(ct);
        if (currentHost == null) return;

        MainThreadDispatcher.Enqueue(() => OnTunnelReady?.Invoke(currentHost));

        if (pollIntervalSeconds <= 0)
        {
            DrainStderr(_process, ct);
            return;
        }

        await PollUntilDead(currentHost, ct);

        if (!ct.IsCancellationRequested)
        {
            UnityEngine.Debug.LogWarning("[CloudflaredTunnel] Quick tunnel unreachable. Use the restart button to try again.");
            KillProcess();
        }
    }

    // ── Named tunnel health ──────────────────────────────────────────────

    private async Task<bool> CheckNamedTunnel(string host, CancellationToken ct)
    {
        int timeoutMs = (int)(namedTunnelTimeoutSeconds * 1000);

        // Mono's TLS stack can fail on some certs. Try HTTPS first, fall back to
        // a raw TCP connect to port 443 which proves the tunnel is alive.
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            var response = await _http.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, $"https://{host}"),
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token
            );
            int code = (int)response.StatusCode;
            bool ok = code >= 200 && code < 500;
            UnityEngine.Debug.Log($"[CloudflaredTunnel] Named tunnel HTTPS check: {code} (ok={ok})");
            return ok;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] HTTPS check failed ({ex.GetType().Name}), trying TCP fallback...");
        }

        // Fallback: just check if port 443 is open (proves tunnel + Cloudflare are up)
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var connectTask = tcp.ConnectAsync(host, 443);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs, ct)) == connectTask && tcp.Connected)
            {
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Named tunnel TCP check OK — {host}:443 is open");
                return true;
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] TCP check also failed: {ex.Message}");
        }

        return false;
    }

    private async Task PollNamedTunnel(string host, CancellationToken ct)
    {
        if (pollIntervalSeconds <= 0) return;

        int failures = 0;
        int intervalMs = (int)(pollIntervalSeconds * 1000);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(intervalMs, ct).ContinueWith(_ => { });
            if (ct.IsCancellationRequested) return;

            bool alive = await PingTunnel($"https://{host}");

            if (alive)
            {
                failures = 0;
            }
            else
            {
                failures++;
                UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Named tunnel health check failed ({failures}/{failuresBeforeRestart}) — {host}");
                if (failures >= failuresBeforeRestart) return;
            }
        }
    }

    // ── Process management ────────────────────────────────────────────────

    private string StartQuickTunnelProcess(CancellationToken ct)
    {
        try
        {
            string args = $"tunnel --url http://localhost:{localPort} --no-autoupdate --protocol http2";

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

            UnityEngine.Debug.Log("[CloudflaredTunnel] Quick tunnel process started");

            return ReadUntilUrl(_process.StandardError, ct);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[CloudflaredTunnel] Failed to start quick tunnel: {ex.Message}");
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

            if (rateLimitRegex.IsMatch(line))
            {
                UnityEngine.Debug.LogError("[CloudflaredTunnel] Rate limited by Cloudflare (429). Use the restart button to try again.");
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
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Quick tunnel ready: {pendingHost}");
                var reader2 = reader;
                var ct2     = ct;
                Task.Run(() => DrainStderr(_process, ct2));
                return pendingHost;
            }
        }

        return null;
    }

    // ── Health polling (quick tunnel) ────────────────────────────────────

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
            int code = (int)response.StatusCode;
            bool ok  = code >= 200 && code < 500;
            if (ok)
                UnityEngine.Debug.Log($"[CloudflaredTunnel] Health check OK — {code}");
            else
                UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Health check bad status — {code}");
            return ok;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[CloudflaredTunnel] Health check error: {ex.Message}");
            return false;
        }
    }
}
