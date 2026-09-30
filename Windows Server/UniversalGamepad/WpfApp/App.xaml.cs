using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UniversalGamepad.Core.Interfaces;
using UniversalGamepad.Core.Services;
using UniversalGamepad.Infrastructure.Network;
using UniversalGamepad.Infrastructure.Services;

namespace UniversalGamepad.WpfApp;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        ServiceProvider = serviceCollection.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IInputListener, UdpInputListener>();
        services.AddSingleton<IDiscoveryBroadcaster, UdpDiscoveryBroadcaster>();
        services.AddSingleton<IGamepadManager, DynamicGamepadManager>();

        services.AddSingleton<EmulatorEngine>();

        services.AddTransient<MainWindow>();
    }
}