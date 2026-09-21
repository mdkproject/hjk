using System.Globalization;

namespace JKalixto_System.Presentation.Converters;

/// <summary>
/// Los DTOs de Application/Services/Servicios.cs (HabitacionCardDto,
/// EstadoHabitacionResumenDto) ya NO exponen un Color/Brush de MAUI
/// directamente — eso amarraba la capa de Application a MAUI, impidiendo que
/// viva en una librería compartida con el futuro proyecto web (Blazor
/// Server). En su lugar exponen una CLAVE de texto (ej. "ColorDisponible"),
/// el mismo nombre que ya usa TemaService como llave de
/// Application.Current.Resources. Estos dos converters son el único lugar de
/// TODA la app MAUI que traduce esa clave a un Color/Brush real, resolviendo
/// el {DynamicResource} de forma manual porque el binding viene desde un
/// value converter, no desde XAML directo.
/// </summary>
public class ClaveColorAColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return ResolverColor(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    internal static Color ResolverColor(string? clave)
    {
        if (!string.IsNullOrEmpty(clave)
            && Microsoft.Maui.Controls.Application.Current?.Resources is { } recursos
            && recursos.TryGetValue(clave, out var valor)
            && valor is Color color)
        {
            return color;
        }

        return Colors.Gray;
    }
}

/// <summary>Igual que <see cref="ClaveColorAColorConverter"/> pero como Brush — Border.Stroke
/// es de tipo Brush, no Color.</summary>
public class ClaveColorABrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return new SolidColorBrush(ClaveColorAColorConverter.ResolverColor(value as string));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
