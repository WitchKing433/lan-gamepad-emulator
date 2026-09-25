using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using UniversalGamepad.Core.Interfaces;
using UniversalGamepad.Core.Models;

namespace UniversalGamepad.Infrastructure.Hardware;

public class VirtualXbox360 : Core.Interfaces.IVirtualGamepad
{
    private readonly IXbox360Controller _controller;

    public VirtualXbox360(ViGEmClient client)
    {
        _controller = client.CreateXbox360Controller();
    }

    public void Connect()
    {
        _controller.Connect();
    }

    public void Disconnect()
    {
        _controller.Disconnect();
    }

    public void Update(GamepadState state)
    {
        _controller.SetButtonState(Xbox360Button.A, (state.Buttons & 1) != 0);
        _controller.SetButtonState(Xbox360Button.B, (state.Buttons & 2) != 0);
        _controller.SetButtonState(Xbox360Button.X, (state.Buttons & 4) != 0);
        _controller.SetButtonState(Xbox360Button.Y, (state.Buttons & 8) != 0);

        _controller.SetButtonState(Xbox360Button.Start, (state.Buttons & 16) != 0);
        _controller.SetButtonState(Xbox360Button.Back, (state.Buttons & 32) != 0);
        _controller.SetButtonState(Xbox360Button.LeftThumb, (state.Buttons & 64) != 0);
        _controller.SetButtonState(Xbox360Button.RightThumb, (state.Buttons & 128) != 0);

        _controller.SetButtonState(Xbox360Button.LeftShoulder, (state.Buttons & 256) != 0);
        _controller.SetButtonState(Xbox360Button.RightShoulder, (state.Buttons & 512) != 0);

        _controller.SetButtonState(Xbox360Button.Up, (state.Buttons & 1024) != 0);
        _controller.SetButtonState(Xbox360Button.Down, (state.Buttons & 2048) != 0);
        _controller.SetButtonState(Xbox360Button.Left, (state.Buttons & 4096) != 0);
        _controller.SetButtonState(Xbox360Button.Right, (state.Buttons & 8192) != 0);

        short xInput = (short)((state.JoyX - 128) * 256);
        short yInput = (short)((state.JoyY - 128) * 256);

        _controller.SetAxisValue(Xbox360Axis.LeftThumbX, xInput);
        _controller.SetAxisValue(Xbox360Axis.LeftThumbY, yInput);

        _controller.SetSliderValue(Xbox360Slider.LeftTrigger, state.LeftTrigger);
        _controller.SetSliderValue(Xbox360Slider.RightTrigger, state.RightTrigger);
    }
}