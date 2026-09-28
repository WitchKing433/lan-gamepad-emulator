using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nefarius.ViGEm.Client;
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
    private readonly bool[] _allocatedSlots = new bool[MaxControllers];

    public event Action<int, bool, GamepadType?>? OnSlotChanged;

    private class ClientSession
    {
        public int SlotIndex { get; set; }
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

                int assignedSlot = -1;
                for (int i = 0; i < MaxControllers; i++)
                {
                    if (!_allocatedSlots[i])
                    {
                        _allocatedSlots[i] = true;
                        assignedSlot = i + 1;
                        break;
                    }
                }

                if (assignedSlot == -1) return;

                byte rawType = packet[3];
                GamepadType type;
                UniversalGamepad.Core.Interfaces.IVirtualGamepad? newGamepad = null;

                if (rawType == 1)
                {
                    type = GamepadType.Xbox360;
                    newGamepad = new VirtualXbox360(_vigemClient);
                }
                else if (rawType == 2)
                {
                    type = GamepadType.DualShock4;
                    newGamepad = new VirtualDualShock4(_vigemClient);
                }
                else
                {
                    _allocatedSlots[assignedSlot - 1] = false;
                    return;
                }

                newGamepad.Connect();

                var session = new ClientSession { SlotIndex = assignedSlot, Gamepad = newGamepad, LastSeen = DateTime.UtcNow };
                if (_sessions.TryAdd(clientIp, session))
                {
                    OnSlotChanged?.Invoke(assignedSlot, true, type);
                }
                else
                {
                    _allocatedSlots[assignedSlot - 1] = false;
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
                            lock (_lockObject)
                            {
                                _allocatedSlots[expiredSession.SlotIndex - 1] = false;
                            }
                            expiredSession.Gamepad.Disconnect();
                            OnSlotChanged?.Invoke(expiredSession.SlotIndex, false, null);
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
            OnSlotChanged?.Invoke(session.SlotIndex, false, null);
        }
        _sessions.Clear();

        lock (_lockObject)
        {
            Array.Clear(_allocatedSlots, 0, _allocatedSlots.Length);
            _vigemClient?.Dispose();
            _vigemClient = null;
        }
    }
}
