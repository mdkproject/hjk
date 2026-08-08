using Microsoft.Extensions.DependencyInjection;

namespace JKalixto_System;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // AppShell se resuelve por DI para que, en cascada, LoginPage y
        // DashboardPage también puedan recibir sus ViewModels por constructor.
        var shell = _serviceProvider.GetRequiredService<AppShell>();

        return new Window(shell)
        {
            Title = "Hotel-Sauna JKalixto"
        };
    }
}
