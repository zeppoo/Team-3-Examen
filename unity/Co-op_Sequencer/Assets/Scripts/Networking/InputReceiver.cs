using System;
using UnityEngine;
using CoopSequencer.Networking;

/// <summary>
/// Translation layer between raw WebSocket messages and typed game input events.
///
/// Subscribe to OnButtonInput / OnScratchInput / OnSliderInput anywhere in the
/// game to react to player input without touching networking code.
///
/// Attach to the same GameObject as WebSocketServer.
/// </summary>
public class InputReceiver : MonoBehaviour
{
    // ── Typed game input events ───────────────────────────────────────────

    /// <summary>Fired when a player presses or releases a button (legacy, kept for SymbolScroller compat).</summary>
    public static event Action<ButtonInputEvent> OnButtonInput;

    /// <summary>Fired when a player moves the scratchpad.</summary>
    public static event Action<ScratchInputEvent> OnScratchInput;

    /// <summary>Fired when a player swipes the DJ slider to switch lane.</summary>
    public static event Action<SliderInputEvent> OnSliderInput;

    /// <summary>Fired when a player submits a symbol + name selection from the mobile select screen.</summary>
    public static event Action<string, SymbolSelectMessage> OnSymbolSelect;

    /// <summary>Fired when a phone sends a reconnect handshake (has a valid cookie for this lobby).</summary>
    public static event Action<string, RejoinMessage> OnRejoin;

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
                    player:  string.IsNullOrEmpty(raw.player) ? clientId : raw.player,
                    button:  raw.button,
                    state:   raw.state == "press" ? ButtonState.Press : ButtonState.Release
                );
                Debug.Log($"[Input] {buttonEvent.player} {buttonEvent.button} {buttonEvent.state}");
                OnButtonInput?.Invoke(buttonEvent);
                break;

            case "scratch":
                var rawScratch = JsonUtility.FromJson<ScratchMessage>(json);
                var scratchEvent = new ScratchInputEvent(
                    player:   string.IsNullOrEmpty(rawScratch.player) ? clientId : rawScratch.player,
                    velocity: rawScratch.velocity
                );
                OnScratchInput?.Invoke(scratchEvent);
                break;

            case "slider":
                var rawSlider = JsonUtility.FromJson<SliderMessage>(json);
                var dir = rawSlider.direction == "up" ? SliderDirection.Up : SliderDirection.Down;
                var sliderEvent = new SliderInputEvent(
                    player:    string.IsNullOrEmpty(rawSlider.player) ? clientId : rawSlider.player,
                    direction: dir
                );
                Debug.Log($"[Input] {sliderEvent.player} slider {sliderEvent.direction}");
                OnSliderInput?.Invoke(sliderEvent);
                break;

            case "symbol_select":
                var rawSymbol = JsonUtility.FromJson<SymbolSelectMessage>(json);
                Debug.Log($"[Input] {clientId} symbol_select symbol={rawSymbol.symbol} name={rawSymbol.name}");
                OnSymbolSelect?.Invoke(clientId, rawSymbol);
                break;

            case "rejoin":
                var rawRejoin = JsonUtility.FromJson<RejoinMessage>(json);
                Debug.Log($"[Input] {clientId} rejoin lobbyId={rawRejoin.lobbyId} token={rawRejoin.reconnectToken}");
                OnRejoin?.Invoke(clientId, rawRejoin);
                break;

            default:
                Debug.LogWarning($"[Input] Unknown message type '{envelope.type}' from {clientId}");
                break;
        }
    }
}
