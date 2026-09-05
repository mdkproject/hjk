using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace JKalixto_System.Application.Services;

/// <summary>
/// Controla el tema visual (oscuro/claro) de toda la aplicación.
///
/// Cómo funciona: los archivos .xaml usan {DynamicResource ColorXxx} en vez de
/// {StaticResource ColorXxx}. La diferencia es que StaticResource lee el valor UNA
/// sola vez (al cargar la pantalla) y nunca más lo actualiza, mientras que
/// DynamicResource se queda "escuchando" ese recurso: si el valor cambia en tiempo
/// de ejecución, todas las pantallas que lo usan se repintan solas, sin necesidad
/// de reiniciar la app ni volver a navegar.
///
/// Por eso alcanza con cambiar los valores acá adentro (AplicarTema) para que todo
/// el sistema cambie de color al instante, sin tocar cada pantalla una por una.
///
/// Nota de namespaces: en este proyecto "Application" es ambiguo (existe el
/// namespace JKalixto_System.Application de Clean Architecture Y la clase
/// Microsoft.Maui.Controls.Application). Por eso, en este archivo SIEMPRE se
/// escribe "Microsoft.Maui.Controls.Application.Current" completo, nunca
/// "Application.Current" a secas, para no repetir el error CS0118 que ya tuvimos
/// una vez en App.xaml.cs.
/// </summary>
public static class TemaService
{
    private const string ClavePreferencia = "TemaOscuro";

    public static bool EsOscuro { get; private set; } = true;

    /// <summary>Se llama una sola vez, al arrancar la app (en App.xaml.cs), antes de crear la primera pantalla.</summary>
    public static void Inicializar()
    {
        EsOscuro = Preferences.Default.Get(ClavePreferencia, true);
        AplicarTema(EsOscuro);
    }

    /// <summary>Cambia entre oscuro y claro, guarda la preferencia, y repinta toda la app al instante.</summary>
    public static void AlternarTema()
    {
        AplicarTema(!EsOscuro);
    }

    public static void AplicarTema(bool oscuro)
    {
        EsOscuro = oscuro;
        Preferences.Default.Set(ClavePreferencia, oscuro);

        var recursos = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (recursos is null)
        {
            return;
        }

        if (oscuro)
        {
            // Tema oscuro — fondo casi negro y líneas de grilla sutiles inspirados en
            // Google Calendar en modo nocturno, pero con el acento DORADO original del
            // sistema (identidad visual del hotel) en vez del azul de Google.
            recursos["ColorFondo"] = Color.FromArgb("#202124");
            recursos["ColorSuperficie"] = Color.FromArgb("#292A2D");
            recursos["ColorTarjeta"] = Color.FromArgb("#303134");
            recursos["ColorBorde"] = Color.FromArgb("#3C4043");
            recursos["ColorAcento"] = Color.FromArgb("#C9A84C");
            recursos["ColorTextoPrimario"] = Color.FromArgb("#E8EAED");
            recursos["ColorTextoSecundario"] = Color.FromArgb("#9AA0A6");
            recursos["ColorDisponible"] = Color.FromArgb("#81C995");
            recursos["ColorOcupada"] = Color.FromArgb("#669DF6");
            recursos["ColorLimpieza"] = Color.FromArgb("#FDD663");
            recursos["ColorMantenimiento"] = Color.FromArgb("#F28B82");
            recursos["ColorReservada"] = Color.FromArgb("#D7AEFB");
            recursos["ColorSaunaDamas"] = Color.FromArgb("#F6AEA9");
        }
        else
        {
            // Tema claro — mismos usos, colores más oscuros que el original para
            // mantener buen contraste sobre fondo blanco.
            recursos["ColorFondo"] = Color.FromArgb("#EEF1F6");
            recursos["ColorSuperficie"] = Color.FromArgb("#FFFFFF");
            recursos["ColorTarjeta"] = Color.FromArgb("#FFFFFF");
            recursos["ColorBorde"] = Color.FromArgb("#D8DEE9");
            // Azul claro en vez del dorado/amarillo original — distinto del azul de
            // "Ocupada" (#2563EB, más oscuro/saturado) para no confundir un botón
            // resaltado con el color de estado de una habitación ocupada.
            recursos["ColorAcento"] = Color.FromArgb("#0EA5E9");
            recursos["ColorTextoPrimario"] = Color.FromArgb("#1A2233");
            recursos["ColorTextoSecundario"] = Color.FromArgb("#64748B");
            recursos["ColorDisponible"] = Color.FromArgb("#16A34A");
            recursos["ColorOcupada"] = Color.FromArgb("#2563EB");
            recursos["ColorLimpieza"] = Color.FromArgb("#D97706");
            recursos["ColorMantenimiento"] = Color.FromArgb("#DC2626");
            recursos["ColorReservada"] = Color.FromArgb("#7C3AED");
            recursos["ColorSaunaDamas"] = Color.FromArgb("#DB2777");
        }
    }
}
