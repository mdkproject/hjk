using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>
/// Clase base para todos los ViewModels del sistema. Da soporte a data binding
/// (que la UI se actualice sola cuando cambia una propiedad) y expone IsBusy/Title,
/// que casi todas las páginas van a necesitar.
/// </summary>
public class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    /// <summary>True mientras hay una operación en curso (ej: validando login). Sirve para mostrar un loading y bloquear botones.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Actualiza el valor de un campo y notifica a la UI SOLO si el valor realmente cambió.
    /// Todas las propiedades de los ViewModels deben usar este método en su "set".
    /// </summary>
    protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingField, value))
        {
            return false;
        }

        backingField = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
