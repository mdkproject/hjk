using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class AlmacenMovimientoViewModel : BaseViewModel
{
    private readonly IInventarioService _inventarioService;
    private readonly ISessionService _sessionService;

    public ObservableCollection<InsumoCardDto> Insumos { get; } = new();

    private InsumoCardDto? _insumoSeleccionado;
    public InsumoCardDto? InsumoSeleccionado
    {
        get => _insumoSeleccionado;
        set
        {
            if (SetProperty(ref _insumoSeleccionado, value))
            {
                OnPropertyChanged(nameof(HaySeleccion));
            }
        }
    }

    public bool HaySeleccion => InsumoSeleccionado is not null;

    private string _tipoTexto = "Entrada";
    public string TipoTexto
    {
        get => _tipoTexto;
        set => SetProperty(ref _tipoTexto, value);
    }

    private string _cantidadTexto = string.Empty;
    public string CantidadTexto
    {
        get => _cantidadTexto;
        set => SetProperty(ref _cantidadTexto, value);
    }

    private string _motivo = string.Empty;
    public string Motivo
    {
        get => _motivo;
        set => SetProperty(ref _motivo, value);
    }

    private string _mensajeError = string.Empty;
    public string MensajeError
    {
        get => _mensajeError;
        set => SetProperty(ref _mensajeError, value);
    }

    private bool _hayError;
    public bool HayError
    {
        get => _hayError;
        set => SetProperty(ref _hayError, value);
    }

    public ICommand SeleccionarTipoCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public AlmacenMovimientoViewModel(IInventarioService inventarioService, ISessionService sessionService)
    {
        _inventarioService = inventarioService;
        _sessionService = sessionService;
        Title = "Nuevo Movimiento de Almacén";

        SeleccionarTipoCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                TipoTexto = tipo;
            }
        });

        ConfirmarCommand = new Command(async () => await ConfirmarAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task CargarAsync()
    {
        if (Insumos.Count > 0)
        {
            return; // el catálogo de insumos no cambia durante el registro del movimiento
        }

        var lista = await _inventarioService.ObtenerInsumosAsync();
        Insumos.Clear();
        foreach (var i in lista)
        {
            Insumos.Add(i);
        }
    }

    private async Task ConfirmarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (InsumoSeleccionado is null)
        {
            MensajeError = "Elegí un insumo de la lista.";
            HayError = true;
            return;
        }

        if (!int.TryParse(CantidadTexto, out var cantidad) || cantidad <= 0)
        {
            MensajeError = "Ingresá una cantidad válida, mayor a cero.";
            HayError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Motivo))
        {
            MensajeError = "El motivo es obligatorio.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
            var tipo = TipoTexto == "Salida" ? TipoMovimientoInventario.Salida : TipoMovimientoInventario.Entrada;

            await _inventarioService.RegistrarMovimientoAsync(new NuevoMovimientoInventarioDto
            {
                InsumoId = InsumoSeleccionado.Id,
                Tipo = tipo,
                Cantidad = cantidad,
                Motivo = Motivo.Trim(),
                UsuarioId = usuarioId
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (System.InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            HayError = true;
        }
        catch (System.Exception)
        {
            MensajeError = "No se pudo registrar el movimiento. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
