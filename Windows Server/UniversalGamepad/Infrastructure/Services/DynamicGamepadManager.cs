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
    private bool _isInitialized;
    private bool _isShuttingDown;

    public event Action<int, bool, GamepadType?>? OnSlotChanged;

    private class ClientSession
    {
        public int SlotIndex { get; set; }
        public UniversalGamepad.Core.Interfaces.IVirtualGamepad Gamepad { get; set; } = null!;
        public DateTime LastSeen { get; set; }
        public object SessionLock { get; } = new();
        public bool IsDisposed { get; set; }
    }

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

    public void Initialize()
    {
        lock (_lockObject)
        {
            if (_isShuttingDown) throw new InvalidOperationException("El manager se está cerrando.");
            if (_isInitialized) return;

            _vigemClient = new ViGEmClient();
            var cleanupCts = new CancellationTokenSource();
            _cts = cleanupCts;
            _cleanupTask = Task.Run(() => CleanupLoopAsync(cleanupCts.Token));

            _isInitialized = true;
        }
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
            byte rawType = packet[3];
            if (rawType is not 1 and not 2) return;

            if (_sessions.Count >= MaxControllers || _sessions.ContainsKey(clientIp)) return;

            lock (_lockObject)
            {
                if (_sessions.Count >= MaxControllers || _sessions.ContainsKey(clientIp)) return;
                if (_vigemClient == null || !_isInitialized || _isShuttingDown) return;

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

                GamepadType type = rawType == 1 ? GamepadType.Xbox360 : GamepadType.DualShock4;
                UniversalGamepad.Core.Interfaces.IVirtualGamepad? newGamepad = null;
                bool sessionAdded = false;
                try
                {
                    newGamepad = rawType == 1
                        ? new VirtualXbox360(_vigemClient)
                        : new VirtualDualShock4(_vigemClient);
                    newGamepad.Connect();

                    var session = new ClientSession { SlotIndex = assignedSlot, Gamepad = newGamepad, LastSeen = DateTime.UtcNow };
                    if (_sessions.TryAdd(clientIp, session))
                    {
                        sessionAdded = true;
                        OnSlotChanged?.Invoke(assignedSlot, true, type);
                    }
                }
                finally
                {
                    if (!sessionAdded)
                    {
                        try
                        {
                            newGamepad?.Disconnect();
                        }
                        catch
                        {
                        }

                        _allocatedSlots[assignedSlot - 1] = false;
                    }
                }
            }
            return;
        }

        if (messageType == 2 && payloadLength == 6 && _sessions.TryGetValue(clientIp, out var activeSession))
        {
            lock (activeSession.SessionLock)
            {
                if (activeSession.IsDisposed) return;

                activeSession.LastSeen = DateTime.UtcNow;

                ushort buttons = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(3, 2));
                var state = new GamepadState(buttons, packet[5], packet[6], packet[7], packet[8]);

                activeSession.Gamepad.Update(state);
            }
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
                    var session = kp.Value;

                    if ((now - session.LastSeen).TotalSeconds > 5)
                    {
                        lock (session.SessionLock)
                        {
                            if ((now - session.LastSeen).TotalSeconds <= 5)
                            {
                                continue;
                            }

                            if (_sessions.TryGetValue(kp.Key, out var currentSession) && ReferenceEquals(currentSession, session))
                            {
                                session.IsDisposed = true;

                                try
                                {
                                    session.Gamepad.Disconnect();
                                }
                                catch
                                {
                                }

                                lock (_lockObject)
                                {
                                    if (_sessions.TryRemove(kp.Key, out var expiredSession))
                                    {
                                        _allocatedSlots[expiredSession.SlotIndex - 1] = false;
                                        OnSlotChanged?.Invoke(expiredSession.SlotIndex, false, null);
                                    }
                                }
                            }
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
        CancellationTokenSource? cleanupCts;
        Task? cleanupTask;

        lock (_lockObject)
        {
            if (!_isInitialized || _isShuttingDown) return;

            _isShuttingDown = true;
            cleanupCts = _cts;
            cleanupTask = _cleanupTask;
        }

        cleanupCts?.Cancel();
        try
        {
            cleanupTask?.GetAwaiter().GetResult();
        }
        catch
        {
        }
        cleanupCts?.Dispose();

        try
        {
            foreach (var session in _sessions.Values)
            {
                lock (session.SessionLock)
                {
                    session.IsDisposed = true;
                    session.Gamepad.Disconnect();
                }
                OnSlotChanged?.Invoke(session.SlotIndex, false, null);
            }
        }
        finally
        {
            lock (_lockObject)
            {
                _sessions.Clear();
                Array.Clear(_allocatedSlots, 0, _allocatedSlots.Length);
                _cleanupTask = null;
                _cts = null;
                _isInitialized = false;
                _isShuttingDown = false;

                var vigemClient = _vigemClient;
                _vigemClient = null;
                vigemClient?.Dispose();
            }
        }
    }
}
