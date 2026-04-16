using System;

/// <summary>
/// Per-player input receiver. Fires typed input events only for its owner.
/// Obtain an instance from LobbyManager.GetPlayerInput(clientId).
/// </summary>
public class PlayerInputReceiver
{
    public readonly Player player;

    public event Action<ButtonInputEvent>  OnButtonInput;
    public event Action<ScratchInputEvent> OnScratchInput;
    public event Action<SliderInputEvent>  OnSliderInput;

    public PlayerInputReceiver(Player player)
    {
        this.player = player;
    }

    internal void DispatchButton(ButtonInputEvent e)   => OnButtonInput?.Invoke(e);
    internal void DispatchScratch(ScratchInputEvent e)  => OnScratchInput?.Invoke(e);
    internal void DispatchSlider(SliderInputEvent e)    => OnSliderInput?.Invoke(e);
}
