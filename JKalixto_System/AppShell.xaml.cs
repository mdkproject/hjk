using JKalixto_System.Presentation.Pages;
using JKalixto_System.Application.Services;
using JKalixto_System.Domain.Models;

namespace JKalixto_System;

public partial class AppShell : Shell
{
    private readonly ISessionService _sessionService;

    public AppShell(ISessionService sessionService)
    {
        InitializeComponent();
        _sessionService = sessionService;

        // Clientes, Registro Hotel, Registro Sauna, Reservas, Cierre de Caja,
        // Reportes y Ver Auditoría son FlyoutItem (pestañas de la barra
        // lateral, declaradas en el XAML), así que YA tienen su ruta
        // registrada automáticamente por Shell — no hay que llamar
        // RegisterRoute para ellas (llamarlo de nuevo tiraría una excepción
        // de ruta duplicada).
        //
        // Login, en cambio, se sacó de la barra lateral (login desactivado
        // temporalmente — ver MauiProgram.cs, ModoPruebaSinLogin) pero se dejó
        // registrada como ruta "suelta" para que "Cerrar Sesión" la siga
        // encontrando, y para el día que se reactive el login de verdad.
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));

        // Estas siguen siendo pantallas "hijas" (se llega a ellas empujando
        // hacia adelante desde alguna pestaña, con flecha de volver
        // automática), así que sí necesitan registrarse acá.
        Routing.RegisterRoute(nameof(CheckInPage), typeof(CheckInPage));
        Routing.RegisterRoute(nameof(ReservaNuevaPage), typeof(ReservaNuevaPage));
        Routing.RegisterRoute(nameof(GastoNuevoPage), typeof(GastoNuevoPage));
        Routing.RegisterRoute(nameof(SaunaRegistroPage), typeof(SaunaRegistroPage));
        Routing.RegisterRoute(nameof(SaunaVentaPage), typeof(SaunaVentaPage));
        Routing.RegisterRoute(nameof(AlmacenMovimientoPage), typeof(AlmacenMovimientoPage));

        // "Ver Auditoría" solo para Gerencia/Desarrollador — se decide una vez
        // acá porque, con el login desactivado, el usuario de la sesión ya
        // está fijo desde que arranca la app (ver MauiProgram.cs).
        var rol = _sessionService.UsuarioActual?.Rol;
        AuditoriaFlyoutItem.IsVisible = rol is RolUsuario.Gerencia or RolUsuario.Desarrollador;
    }
}
