/// <summary>Button state sent with every button message.</summary>
public enum ButtonState
{
    Press,
    Release
}

/// <summary>
/// Typed event raised when a player presses or releases a button.
/// Game logic subscribes to InputReceiver.OnButtonInput to receive these.
/// </summary>
public readonly struct ButtonInputEvent
{
    /// <summary>ClientId of the player who triggered this input.</summary>
    public readonly string player;

    /// <summary>Which button was pressed, e.g. "button1", "button2".</summary>
    public readonly string button;

    /// <summary>Whether the button was pressed or released.</summary>
    public readonly ButtonState state;

    public ButtonInputEvent(string player, string button, ButtonState state)
    {
        this.player = player;
        this.button = button;
        this.state  = state;
    }
}

/// <summary>
/// Typed event raised when a player moves the scratchpad.
/// Game logic subscribes to InputReceiver.OnScratchInput to receive these.
/// </summary>
public readonly struct ScratchInputEvent
{
    /// <summary>ClientId of the player who triggered this input.</summary>
    public readonly string player;

    /// <summary>Pixels per frame. Negative = backward.</summary>
    public readonly float velocity;

    public ScratchInputEvent(string player, float velocity)
    {
        this.player   = player;
        this.velocity = velocity;
    }
}
