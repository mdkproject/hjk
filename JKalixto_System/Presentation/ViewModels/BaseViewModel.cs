using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>
/// Clase base para todos los ViewModels del sistema. Da soporte a data binding
/// (que la UI se actualice sola cuando cambia una propiedad) y expone IsBusy/Title,
/// que casi todas las páginas van a necesitar.
///
/// También centraliza el reloj/fecha y el botón de alternar tema (antes vivían
/// duplicados solo en ClientesViewModel): cualquier pantalla que quiera mostrar la
/// barra de reloj+tema (ver Presentation/Controls/BarraRelojTemaView.xaml) solo
/// necesita llamar IniciarReloj() en OnAppearing y DetenerReloj() en OnDisappearing —
/// las propiedades RelojTexto/FechaTexto/TemaIconoTexto y el comando
/// AlternarTemaCommand ya están disponibles por herencia.
/// </summary>
public class BaseViewModel : INotifyPropertyChanged
{
    private static readonly string[] DiasSemana =
        { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };

    private static readonly string[] Meses =
        { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio",
          "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

    public event PropertyChangedEventHandler? PropertyChanged;

    private IDispatcherTimer? _timerReloj;

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

    private string _relojTexto = string.Empty;
    public string RelojTexto
    {
        get => _relojTexto;
        set => SetProperty(ref _relojTexto, value);
    }

    private string _fechaTexto = string.Empty;
    public string FechaTexto
    {
        get => _fechaTexto;
        set => SetProperty(ref _fechaTexto, value);
    }

    private string _temaIconoTexto = string.Empty;
    public string TemaIconoTexto
    {
        get => _temaIconoTexto;
        set => SetProperty(ref _temaIconoTexto, value);
    }

    public ICommand AlternarTemaCommand { get; }

    public BaseViewModel()
    {
        AlternarTemaCommand = new Command(() =>
        {
            TemaService.AlternarTema();
            ActualizarIconoTema();
        });
    }

    /// <summary>Arranca el reloj (se actualiza cada segundo) y fija el ícono de tema
    /// actual. Llamar desde OnAppearing de la página que muestre BarraRelojTemaView.</summary>
    public void IniciarReloj()
    {
        ActualizarReloj();
        ActualizarIconoTema();

        DetenerReloj();
        _timerReloj = Microsoft.Maui.Controls.Application.Current?.Dispatcher.CreateTimer();
        if (_timerReloj is not null)
        {
            _timerReloj.Interval = TimeSpan.FromSeconds(1);
            _timerReloj.Tick += (s, e) => ActualizarReloj();
            _timerReloj.Start();
        }
    }

    /// <summary>Detiene el reloj — llamar desde OnDisappearing, para que nunca quede
    /// más de un timer corriendo en segundo plano.</summary>
    public void DetenerReloj()
    {
        _timerReloj?.Stop();
        _timerReloj = null;
    }

    public void ActualizarReloj()
    {
        var ahora = DateTime.Now;
        RelojTexto = ahora.ToString("HH:mm:ss");
        FechaTexto = $"{DiasSemana[(int)ahora.DayOfWeek]} {ahora.Day:00} de {Meses[ahora.Month]} del {ahora.Year}";
    }

    private void ActualizarIconoTema()
    {
        TemaIconoTexto = TemaService.EsOscuro ? "☀️" : "🌙";
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
