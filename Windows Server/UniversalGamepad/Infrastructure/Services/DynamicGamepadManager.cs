using Nefarius.ViGEm.Client;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UniversalGamepad.Core.Enums;
using UniversalGamepad.Core.Interfaces;
using UniversalGamepad.Core.Models;
using UniversalGamepad.Infrastructure.Hardware;

namespace UniversalGamepad.Infrastructure.Services;

public class DynamicGamepadManager : IGamepadManager
{
    private ViGEmClient? _vigemClient;
    private const int MaxControllers = 4;
    private const byte ProtocolMagicByte = 0x54;
    private readonly object _lockObject = new();
    private CancellationTokenSource? _cts;
    private Task? _cleanupTask;

    private class ClientSession
    {
        public UniversalGamepad.Core.Interfaces.IVirtualGamepad Gamepad { get; set; } = null!;
        public DateTime LastSeen { get; set; }
    }

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    public void Initialize()
    {
        _vigemClient = new ViGEmClient();
        _cts = new CancellationTokenSource();
        _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
    }

    public void HandleClientPacket(string clientIp, ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 3) return;

        if (packet[0] != ProtocolMagicByte) return;

        byte messageType = packet[1];
        byte payloadLength = packet[2];

        if (packet.Length != 3 + payloadLength) return;

        if (messageType == 1 && payloadLength == 1)
        {
            if (_sessions.Count >= MaxControllers || _sessions.ContainsKey(clientIp)) return;

            lock (_lockObject)
            {
                if (_sessions.Count >= MaxControllers || _sessions.ContainsKey(clientIp)) return;
                if (_vigemClient == null) return;

                GamepadType type = (GamepadType)packet[3];
                UniversalGamepad.Core.Interfaces.IVirtualGamepad? newGamepad = type switch
                {
                    GamepadType.Xbox360 => new VirtualXbox360(_vigemClient),
                    GamepadType.DualShock4 => new VirtualDualShock4(_vigemClient),
                    _ => null
                };

                if (newGamepad == null) return;

                newGamepad.Connect();

                var session = new ClientSession { Gamepad = newGamepad, LastSeen = DateTime.UtcNow };
                if (!_sessions.TryAdd(clientIp, session))
                {
                    newGamepad.Disconnect();
                }
            }
            return;
        }

        if (messageType == 2 && payloadLength == 6 && _sessions.TryGetValue(clientIp, out var activeSession))
        {
            activeSession.LastSeen = DateTime.UtcNow;

            ushort buttons = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(3, 2));
            var state = new GamepadState(buttons, packet[5], packet[6], packet[7], packet[8]);

            activeSession.Gamepad.Update(state);
        }
    }


    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);
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
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
            }
        }
    }

    public void Shutdown()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try
            {
                _cleanupTask?.GetAwaiter().GetResult();
            }
            catch
            {
            }
            _cts.Dispose();
            _cts = null;
        }

        foreach (var session in _sessions.Values)
        {
            session.Gamepad.Disconnect();
        }
        _sessions.Clear();

        lock (_lockObject)
        {
            _vigemClient?.Dispose();
            _vigemClient = null;
        }
    }
}
