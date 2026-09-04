using Microsoft.Extensions.DependencyInjection;

namespace JKalixto_System;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Aplica el tema guardado (oscuro por defecto la primera vez que se abre
        // la app) ANTES de crear la ventana, para que la primera pantalla ya
        // aparezca con el color correcto y no haya un "parpadeo" de tema oscuro
        // seguido de un cambio a claro.
        JKalixto_System.Application.Services.TemaService.Inicializar();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // AppShell se resuelve por DI para que, en cascada, LoginPage y
        // DashboardPage también puedan recibir sus ViewModels por constructor.
        var shell = _serviceProvider.GetRequiredService<AppShell>();

        return new Window(shell)
        {
            Title = "HKX Hotel & Sauna System"
        };
    }
}
