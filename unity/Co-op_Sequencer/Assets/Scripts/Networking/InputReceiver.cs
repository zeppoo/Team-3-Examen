using UnityEngine;

/// <summary>
/// Logs all button input received from connected phones.
/// Attach to the same GameObject as WebSocketServer.
/// </summary>
public class InputReceiver : MonoBehaviour
{
    private WebSocketServer _server;

    void Awake()
    {
        _server = GetComponent<WebSocketServer>();
    }

    void OnEnable()
    {
        _server.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        _server.OnMessageReceived -= HandleMessage;
    }

    private void HandleMessage(string clientId, string json)
    {
        // Expected: {"button":"pad1","state":"press"}
        var msg = JsonUtility.FromJson<ButtonMessage>(json);
        if (msg == null) return;

        Debug.Log($"[Input] {clientId} → button={msg.button} state={msg.state}");
    }

    [System.Serializable]
    private class ButtonMessage
    {
        public string button;
        public string state;
    }
}
