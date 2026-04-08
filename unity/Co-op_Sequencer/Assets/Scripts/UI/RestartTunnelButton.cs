using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to a UI Button. Kills the current cloudflare tunnel and starts a fresh one.
/// </summary>
public class RestartTunnelButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private CloudflaredTunnel _tunnel;

    void Awake()
    {
        _tunnel = FindFirstObjectByType<CloudflaredTunnel>();

        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(Restart);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Restart);
    }

    private void Restart()
    {
        if (_tunnel == null)
        {
            Debug.LogError("[RestartTunnelButton] No CloudflaredTunnel found.");
            return;
        }

        Debug.Log("[RestartTunnelButton] Restarting tunnel...");
        _tunnel.RestartTunnel();
    }
}
