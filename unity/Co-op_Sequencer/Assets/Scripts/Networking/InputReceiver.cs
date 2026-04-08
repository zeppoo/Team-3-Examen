using System;
using UnityEngine;
using CoopSequencer.Networking;

/// <summary>
/// Translation layer between raw WebSocket messages and typed game input events.
///
/// Subscribe to OnButtonInput / OnScratchInput anywhere in the game to react
/// to player input without touching networking code.
///
/// Attach to the same GameObject as WebSocketServer.
/// </summary>
public class InputReceiver : MonoBehaviour
{
    // ── Typed game input events ───────────────────────────────────────────

    /// <summary>Fired when a player presses or releases a button.</summary>
    public static event Action<ButtonInputEvent> OnButtonInput;

    /// <summary>Fired when a player moves the scratchpad.</summary>
    public static event Action<ScratchInputEvent> OnScratchInput;

    // ─────────────────────────────────────────────────────────────────────

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
            case "button":
                var raw = JsonUtility.FromJson<ButtonMessage>(json);
                var buttonEvent = new ButtonInputEvent(
                    player:  raw.player ?? clientId,
                    button:  raw.button,
                    state:   raw.state == "press" ? ButtonState.Press : ButtonState.Release
                );
                Debug.Log($"[Input] {buttonEvent.player} {buttonEvent.button} {buttonEvent.state}");
                OnButtonInput?.Invoke(buttonEvent);
                break;

            case "scratch":
                var rawScratch = JsonUtility.FromJson<ScratchMessage>(json);
                var scratchEvent = new ScratchInputEvent(
                    player:   rawScratch.player ?? clientId,
                    velocity: rawScratch.velocity
                );
                Debug.Log($"[Input] {scratchEvent.player} scratch velocity={scratchEvent.velocity:F2}");
                OnScratchInput?.Invoke(scratchEvent);
                break;

            default:
                Debug.LogWarning($"[Input] Unknown message type '{envelope.type}' from {clientId}");
                break;
        }
    }
}
