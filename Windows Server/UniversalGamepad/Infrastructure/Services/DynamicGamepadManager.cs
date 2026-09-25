using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Nefarius.ViGEm.Client;
using UniversalGamepad.Core.Enums;
using UniversalGamepad.Core.Interfaces;
using UniversalGamepad.Core.Models;
using UniversalGamepad.Infrastructure.Hardware;

namespace UniversalGamepad.Infrastructure.Services;

public class DynamicGamepadManager : IGamepadManager
{
    private ViGEmClient _vigemClient;
    private const int MaxControllers = 4;

    private class ClientSession
    {
        public UniversalGamepad.Core.Interfaces.IVirtualGamepad Gamepad { get; set; }
        public DateTime LastSeen { get; set; }
    }

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    public void Initialize()
    {
        _vigemClient = new ViGEmClient();
        Task.Run(CleanupLoopAsync);
    }

    public void HandleClientPacket(string clientIp, ReadOnlySpan<byte> packet)
    {
        if (packet.Length == 1)
        {
            if (_sessions.Count >= MaxControllers || _sessions.ContainsKey(clientIp)) return;

            GamepadType type = (GamepadType)packet[0];
            UniversalGamepad.Core.Interfaces.IVirtualGamepad newGamepad = type switch
            {
                GamepadType.Xbox360 => new VirtualXbox360(_vigemClient),
                GamepadType.DualShock4 => new VirtualDualShock4(_vigemClient),
                _ => null
            };

            if (newGamepad == null) return;

            newGamepad.Connect();

            var session = new ClientSession { Gamepad = newGamepad, LastSeen = DateTime.UtcNow };
            _sessions.TryAdd(clientIp, session);
            return;
        }

        if (packet.Length >= 6 && _sessions.TryGetValue(clientIp, out var activeSession))
        {
            activeSession.LastSeen = DateTime.UtcNow;

            ushort buttons = BitConverter.ToUInt16(packet.Slice(0, 2));
            var state = new GamepadState(buttons, packet[2], packet[3], packet[4], packet[5]);

            activeSession.Gamepad.Update(state);
        }
    }

    private async Task CleanupLoopAsync()
    {
        while (_vigemClient != null)
        {
            await Task.Delay(2000);
            var now = DateTime.UtcNow;

            foreach (var kp in _sessions.ToList())
            {
                if ((now - kp.Value.LastSeen).TotalSeconds > 5)
                {
                    if (_sessions.TryRemove(kp.Key, out var expiredSession))
                    {
                        expiredSession.Gamepad.Disconnect();
                    }
                }
            }
        }
    }

    public void Shutdown()
    {
        foreach (var session in _sessions.Values)
        {
            session.Gamepad.Disconnect();
        }
        _sessions.Clear();
        _vigemClient?.Dispose();
        _vigemClient = null;
    }
}