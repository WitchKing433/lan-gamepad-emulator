using System;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.DualShock4;
using UniversalGamepad.Core.Models;

namespace UniversalGamepad.Infrastructure.Hardware;

public class VirtualDualShock4 : UniversalGamepad.Core.Interfaces.IVirtualGamepad
{
    private readonly IDualShock4Controller _controller;

    public VirtualDualShock4(ViGEmClient client)
    {
        _controller = client.CreateDualShock4Controller();
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
        _controller.SetButtonState(DualShock4Button.Cross, (state.Buttons & 1) != 0);
        _controller.SetButtonState(DualShock4Button.Circle, (state.Buttons & 2) != 0);
        _controller.SetButtonState(DualShock4Button.Square, (state.Buttons & 4) != 0);
        _controller.SetButtonState(DualShock4Button.Triangle, (state.Buttons & 8) != 0);

        _controller.SetButtonState(DualShock4Button.Options, (state.Buttons & 16) != 0);
        _controller.SetButtonState(DualShock4Button.Share, (state.Buttons & 32) != 0);
        _controller.SetButtonState(DualShock4Button.ThumbLeft, (state.Buttons & 64) != 0);
        _controller.SetButtonState(DualShock4Button.ThumbRight, (state.Buttons & 128) != 0);

        _controller.SetButtonState(DualShock4Button.ShoulderLeft, (state.Buttons & 256) != 0);
        _controller.SetButtonState(DualShock4Button.ShoulderRight, (state.Buttons & 512) != 0);

        bool up = (state.Buttons & 1024) != 0;
        bool down = (state.Buttons & 2048) != 0;
        bool left = (state.Buttons & 4096) != 0;
        bool right = (state.Buttons & 8192) != 0;

        if (up && right) _controller.SetDPadDirection(DualShock4DPadDirection.Northeast);
        else if (up && left) _controller.SetDPadDirection(DualShock4DPadDirection.Northwest);
        else if (down && right) _controller.SetDPadDirection(DualShock4DPadDirection.Southeast);
        else if (down && left) _controller.SetDPadDirection(DualShock4DPadDirection.Southwest);
        else if (up) _controller.SetDPadDirection(DualShock4DPadDirection.North);
        else if (down) _controller.SetDPadDirection(DualShock4DPadDirection.South);
        else if (left) _controller.SetDPadDirection(DualShock4DPadDirection.West);
        else if (right) _controller.SetDPadDirection(DualShock4DPadDirection.East);
        else _controller.SetDPadDirection(DualShock4DPadDirection.None);

        _controller.SetAxisValue(DualShock4Axis.LeftThumbX, state.JoyX);
        _controller.SetAxisValue(DualShock4Axis.LeftThumbY, state.JoyY);

        _controller.SetSliderValue(DualShock4Slider.LeftTrigger, state.LeftTrigger);
        _controller.SetSliderValue(DualShock4Slider.RightTrigger, state.RightTrigger);
    }
}
