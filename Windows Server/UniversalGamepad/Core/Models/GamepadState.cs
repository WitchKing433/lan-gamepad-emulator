namespace UniversalGamepad.Core.Models;

public readonly record struct GamepadState(
    ushort Buttons,
    byte JoyX,
    byte JoyY,
    byte LeftTrigger,
    byte RightTrigger
);