using JKalixto_System.Presentation.Pages;

namespace JKalixto_System;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Login y Dashboard son ShellContent (rutas raíz, declaradas en AppShell.xaml).
        // Estas páginas, en cambio, se navegan "hacia adelante" (con back automático
        // en la barra superior de Shell), así que se registran aquí en vez de en el XAML.
        Routing.RegisterRoute(nameof(RecepcionPage), typeof(RecepcionPage));
        Routing.RegisterRoute(nameof(CheckInPage), typeof(CheckInPage));
    }
}
