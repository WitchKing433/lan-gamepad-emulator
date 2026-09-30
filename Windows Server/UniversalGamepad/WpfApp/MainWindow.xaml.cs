using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using UniversalGamepad.Core.Enums;
using UniversalGamepad.Core.Interfaces;
using UniversalGamepad.Core.Services;

namespace UniversalGamepad.WpfApp;

public partial class MainWindow : Window
{
    private readonly EmulatorEngine _engine;
    private bool _isServerRunning;
    private bool _isDiscoveryRunning;
    private int _activeConnectionsCount;
    private int _packetProcessingErrorReported;
    private const int ServerPort = 55555;
    private const int DiscoveryPort = 55554;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DwmwaUseImmersiveDarkMode = 20;

    public MainWindow(EmulatorEngine engine)
    {
        InitializeComponent();
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        RefreshNetworkIp();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var windowInteropHelper = new WindowInteropHelper(this);
        IntPtr hwnd = windowInteropHelper.Handle;

        int useDarkMode = 1;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDarkMode, sizeof(int));
    }

    private void BtnToggleServer_Click(object sender, RoutedEventArgs e)
    {
        if (!_isServerRunning)
        {
            StartServerProcess();
        }
        else
        {
            StopServerProcess();
        }
    }

    private void BtnToggleDiscovery_Click(object sender, RoutedEventArgs e)
    {
        if (!_isServerRunning)
        {
            MessageBox.Show("Cannot enable network discovery while emulation is inactive. Please start emulation first.", "Discovery Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_isDiscoveryRunning)
        {
            StartDiscoveryProcess();
        }
        else
        {
            StopDiscoveryProcess();
        }
    }


    private void BtnRefreshIp_Click(object sender, RoutedEventArgs e)
    {
        RefreshNetworkIp();
    }

    private void StartServerProcess()
    {
        try
        {
            Interlocked.Exchange(ref _packetProcessingErrorReported, 0);
            var listener = App.ServiceProvider.GetRequiredService<IInputListener>();
            listener.OnErrorOccurred += HandleNetworkError;
            listener.OnPacketProcessingError += HandlePacketProcessingError;

            var broadcaster = App.ServiceProvider.GetRequiredService<IDiscoveryBroadcaster>();
            broadcaster.OnErrorOccurred += HandleBroadcastError;

            var manager = App.ServiceProvider.GetRequiredService<IGamepadManager>();
            manager.OnSlotChanged += HandleSlotUIChanged;

            _engine.StartEngine(ServerPort);
            _isServerRunning = true;

            BtnToggleServer.Content = "Stop Emulation";
            BtnToggleServer.Background = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            TxtStatus.Text = "Server Active";

            if (!_isDiscoveryRunning)
            {
                StartDiscoveryProcess();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StopServerProcess();
        }
    }

    private void StopServerProcess()
    {
        var listener = App.ServiceProvider.GetService<IInputListener>();
        if (listener != null)
        {
            listener.OnErrorOccurred -= HandleNetworkError;
            listener.OnPacketProcessingError -= HandlePacketProcessingError;
        }

        var broadcaster = App.ServiceProvider.GetService<IDiscoveryBroadcaster>();
        if (broadcaster != null)
        {
            broadcaster.OnErrorOccurred -= HandleBroadcastError;
        }

        var manager = App.ServiceProvider.GetService<IGamepadManager>();
        if (manager != null)
        {
            manager.OnSlotChanged -= HandleSlotUIChanged;
        }

        _engine.StopEngine();
        _isServerRunning = false;

        BtnToggleServer.Content = "Start Emulation";
        BtnToggleServer.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
        TxtStatus.Text = "Server Inactive";

        if (_isDiscoveryRunning)
        {
            StopDiscoveryProcess();
        }

        ResetAllSlotsUI();
    }

    private void HandlePacketProcessingError(Exception ex)
    {
        Trace.TraceError($"UDP packet processing failed: {ex}");

        if (Interlocked.Exchange(ref _packetProcessingErrorReported, 1) != 0) return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_isServerRunning) return;

            TxtStatus.Text = "Input processing error (see diagnostics)";
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 159, 10));
        }));
    }

    private void HandleBroadcastError(Exception ex)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            StopDiscoveryProcess();
            MessageBox.Show($"Network discovery failed:\n{ex.Message}", "Broadcast Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }));
    }

    private void StartDiscoveryProcess()
    {
        try
        {
            _engine.StartBroadcast(DiscoveryPort);
            _isDiscoveryRunning = true;

            BtnToggleDiscovery.Content = "Disable Discovery";
            BtnToggleDiscovery.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
            DiscoveryIndicator.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            TxtDiscovery.Text = "Discovery Enabled";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Discovery Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StopDiscoveryProcess();
        }
    }

    private void StopDiscoveryProcess()
    {
        _engine.StopBroadcast();
        _isDiscoveryRunning = false;

        BtnToggleDiscovery.Content = "Enable Discovery";
        BtnToggleDiscovery.Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
        DiscoveryIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
        TxtDiscovery.Text = "Discovery Disabled";
    }

    private void HandleNetworkError(Exception ex)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            StopServerProcess();
            MessageBox.Show($"Network server failed unexpectedly:\n{ex.Message}", "Critical Network Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }));
    }

    private void HandleSlotUIChanged(int slotIndex, bool isConnected, GamepadType? type)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateSlotVisuals(slotIndex, isConnected, type);
            UpdateTotalConnectedText();
        }));
    }

    private void UpdateSlotVisuals(int slotIndex, bool isConnected, GamepadType? type)
    {
        var border = slotIndex switch
        {
            1 => SlotBorder1,
            2 => SlotBorder2,
            3 => SlotBorder3,
            4 => SlotBorder4,
            _ => null
        };

        var icon = slotIndex switch
        {
            1 => SlotIcon1,
            2 => SlotIcon2,
            3 => SlotIcon3,
            4 => SlotIcon4,
            _ => null
        };

        var txt = slotIndex switch
        {
            1 => SlotTxt1,
            2 => SlotTxt2,
            3 => SlotTxt3,
            4 => SlotTxt4,
            _ => null
        };

        if (border == null || icon == null || txt == null) return;

        if (isConnected && type.HasValue)
        {
            if (type.Value == GamepadType.Xbox360)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(16, 124, 17));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(26, 144, 27));
                icon.Text = "🎮";
                txt.Text = "Xbox 360 Mode";
                txt.Foreground = Brushes.White;
            }
            else if (type.Value == GamepadType.DualShock4)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0, 67, 156));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(10, 87, 176));
                icon.Text = "🎮";
                txt.Text = "DualShock 4 Mode";
                txt.Foreground = Brushes.White;
            }
        }
        else
        {
            border.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            icon.Text = "🎮";
            txt.Text = "Disconnected";
            txt.Foreground = new SolidColorBrush(Color.FromRgb(68, 68, 68));
        }
    }

    private void UpdateTotalConnectedText()
    {
        int count = 0;
        if (SlotTxt1.Text != "Disconnected") count++;
        if (SlotTxt2.Text != "Disconnected") count++;
        if (SlotTxt3.Text != "Disconnected") count++;
        if (SlotTxt4.Text != "Disconnected") count++;

        _activeConnectionsCount = count;
        TxtTotalConnected.Text = $"Total Connected: {_activeConnectionsCount} / 4";
    }

    private void ResetAllSlotsUI()
    {
        for (int i = 1; i <= 4; i++)
        {
            UpdateSlotVisuals(i, false, null);
        }
        UpdateTotalConnectedText();
    }

    private void RefreshNetworkIp()
    {
        TxtIpAddress.Text = GetLocalIPAddress();
    }

    private string GetLocalIPAddress()
    {
        var candidate = NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType is
                    NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .SelectMany(networkInterface =>
            {
                var properties = networkInterface.GetIPProperties();

                bool hasIpv4Gateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                    !gateway.Address.Equals(IPAddress.Any));

                return properties.UnicastAddresses
                    .Where(address => IsUsablePrivateIPv4(address.Address))
                    .Select(address => new
                    {
                        address.Address,
                        HasIpv4Gateway = hasIpv4Gateway,
                        IsWifi = networkInterface.NetworkInterfaceType ==
                                 NetworkInterfaceType.Wireless80211
                    });
            })
            .OrderByDescending(candidate => candidate.HasIpv4Gateway)
            .ThenByDescending(candidate => candidate.IsWifi)
            .ThenBy(candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .FirstOrDefault();

        return candidate?.Address.ToString() ?? "Not available";
    }

    private static bool IsUsablePrivateIPv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            IPAddress.IsLoopback(address))
        {
            return false;
        }

        byte[] bytes = address.GetAddressBytes();

        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopServerProcess();
        base.OnClosed(e);
    }
}
