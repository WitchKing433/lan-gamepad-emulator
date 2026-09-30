using System;
using UniversalGamepad.Core.Interfaces;

namespace UniversalGamepad.Core.Services;

public class EmulatorEngine
{
    private readonly object _lifecycleLock = new();
    private readonly IInputListener _listener;
    private readonly IGamepadManager _gamepadManager;
    private readonly IDiscoveryBroadcaster _broadcaster;
    private bool _isRunning;

    public EmulatorEngine(IInputListener listener, IGamepadManager gamepadManager, IDiscoveryBroadcaster broadcaster)
    {
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _gamepadManager = gamepadManager ?? throw new ArgumentNullException(nameof(gamepadManager));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
    }

    public void StartEngine(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        lock (_lifecycleLock)
        {
            if (_isRunning) return;

            try
            {
                _gamepadManager.Initialize();
                _listener.OnPacketReceived -= ProcessRawPacket;
                _listener.OnPacketReceived += ProcessRawPacket;
                _listener.Start(port);
                _isRunning = true;
            }
            catch
            {
                _listener.OnPacketReceived -= ProcessRawPacket;
                try
                {
                    _listener.Stop();
                }
                finally
                {
                    _gamepadManager.Shutdown();
                }

                throw;
            }
        }
    }

    public void StopEngine()
    {
        lock (_lifecycleLock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener.OnPacketReceived -= ProcessRawPacket;
            try
            {
                _listener.Stop();
            }
            finally
            {
                _gamepadManager.Shutdown();
            }
        }
    }

    public void StartBroadcast(int port)
    {
        _broadcaster.Start(port);
    }

    public void StopBroadcast()
    {
        _broadcaster.Stop();
    }

    private void ProcessRawPacket(string clientIp, ReadOnlyMemory<byte> buffer)
    {
        _gamepadManager.HandleClientPacket(clientIp, buffer.Span);
    }
}
