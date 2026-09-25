using System;
using UniversalGamepad.Core.Interfaces;

namespace UniversalGamepad.Core.Services;

public class EmulatorEngine
{
    private readonly IInputListener _listener;
    private readonly IGamepadManager _gamepadManager;

    public EmulatorEngine(IInputListener listener, IGamepadManager gamepadManager)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _gamepadManager = gamepadManager ?? throw new ArgumentNullException(nameof(gamepadManager));
    }

    public void StartEngine(int port)
    {
        _gamepadManager.Initialize();
        _listener.OnPacketReceived += ProcessRawPacket;
        _listener.Start(port);
    }

    public void StopEngine()
    {
        _listener.Stop();
        _listener.OnPacketReceived -= ProcessRawPacket;
        _gamepadManager.Shutdown();
    }

    private void ProcessRawPacket(string clientIp, ReadOnlyMemory<byte> buffer)
    {
        _gamepadManager.HandleClientPacket(clientIp, buffer.Span);
    }
}
