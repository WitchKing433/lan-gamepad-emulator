using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using UniversalGamepad.Core.Services;

namespace UniversalGamepad.WpfApp;

public partial class MainWindow : Window
{
    private readonly EmulatorEngine _engine;
    private bool _isServerRunning;
    private bool _isDiscoveryRunning;
    private const int ServerPort = 55555;

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
            _engine.StartEngine(ServerPort);
            _isServerRunning = true;

            BtnToggleServer.Content = "Stop Emulation";
            BtnToggleServer.Background = new SolidColorBrush(Color.FromRgb(255, 59, 48));
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(52, 199, 89));
            TxtStatus.Text = "Server Active";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StopServerProcess();
        }
    }

    private void StopServerProcess()
    {
        _engine.StopEngine();
        _isServerRunning = false;

        BtnToggleServer.Content = "Start Emulation";
        BtnToggleServer.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204));
        StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 59, 48));
        TxtStatus.Text = "Server Inactive";
    }

    private void StartDiscoveryProcess()
    {
        try
        {
            _engine.StartBroadcast(ServerPort);
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

    private void RefreshNetworkIp()
    {
        TxtIpAddress.Text = GetLocalIPAddress();
    }

    private string GetLocalIPAddress()
    {
        foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (netInterface.OperationalStatus == OperationalStatus.Up &&
                netInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                var props = netInterface.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        return "127.0.0.1";
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_isServerRunning) _engine.StopEngine();
        if (_isDiscoveryRunning) _engine.StopBroadcast();
        base.OnClosed(e);
    }
}
