using UnityEngine;
using CoopSequencer.Networking;

/// <summary>
/// Receives and dispatches controller input from connected phones.
/// Attach to the same GameObject as WebSocketServer.
/// Message schema is defined in ControllerMessages.cs and mirrored in
/// mobile/controller-app/src/lib/messages.ts.
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
        var envelope = JsonUtility.FromJson<MessageEnvelope>(json);
        if (envelope == null) return;

        switch (envelope.type)
        {
            case "pad":
                var pad = JsonUtility.FromJson<PadMessage>(json);
                Debug.Log($"[Input] {clientId} pad id={pad.id} state={pad.state}");
                OnPad(clientId, pad);
                break;

            case "scratch":
                var scratch = JsonUtility.FromJson<ScratchMessage>(json);
                Debug.Log($"[Input] {clientId} scratch velocity={scratch.velocity:F2}");
                OnScratch(clientId, scratch);
                break;

            default:
                Debug.LogWarning($"[Input] Unknown message type '{envelope.type}' from {clientId}");
                break;
        }
    }

    // Override these in a subclass or replace with UnityEvents / your game logic.
    protected virtual void OnPad(string clientId, PadMessage msg) { }
    protected virtual void OnScratch(string clientId, ScratchMessage msg) { }
}
