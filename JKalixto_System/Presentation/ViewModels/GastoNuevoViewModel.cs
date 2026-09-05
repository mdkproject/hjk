using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class GastoNuevoViewModel : BaseViewModel
{
    private readonly IGastosService _gastosService;
    private readonly ISessionService _sessionService;

    private string _direccionTexto = "Salida";
    public string DireccionTexto
    {
        get => _direccionTexto;
        set => SetProperty(ref _direccionTexto, value);
    }

    private string _categoriaTexto = "Gastos diarios";
    public string CategoriaTexto
    {
        get => _categoriaTexto;
        set => SetProperty(ref _categoriaTexto, value);
    }

    private string _origenTexto = "Hotel";
    public string OrigenTexto
    {
        get => _origenTexto;
        set => SetProperty(ref _origenTexto, value);
    }

    private string _descripcion = string.Empty;
    public string Descripcion
    {
        get => _descripcion;
        set => SetProperty(ref _descripcion, value);
    }

    private string _personalRelacionado = string.Empty;
    public string PersonalRelacionado
    {
        get => _personalRelacionado;
        set => SetProperty(ref _personalRelacionado, value);
    }

    private string _montoTexto = string.Empty;
    public string MontoTexto
    {
        get => _montoTexto;
        set => SetProperty(ref _montoTexto, value);
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

    public ICommand SeleccionarDireccionCommand { get; }
    public ICommand SeleccionarCategoriaCommand { get; }
    public ICommand SeleccionarOrigenCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public GastoNuevoViewModel(IGastosService gastosService, ISessionService sessionService)
    {
        _gastosService = gastosService;
        _sessionService = sessionService;
        Title = "Nuevo Movimiento de Caja";

        SeleccionarDireccionCommand = new Command<string>((valor) =>
        {
            if (valor is not null)
            {
                DireccionTexto = valor;
            }
        });

        SeleccionarCategoriaCommand = new Command<string>((valor) =>
        {
            if (valor is not null)
            {
                CategoriaTexto = valor;
            }
        });

        SeleccionarOrigenCommand = new Command<string>((valor) =>
        {
            if (valor is not null)
            {
                OrigenTexto = valor;
            }
        });

        ConfirmarCommand = new Command(async () => await ConfirmarAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    private async Task ConfirmarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(Descripcion))
        {
            MensajeError = "La descripción es obligatoria.";
            HayError = true;
            return;
        }

        if (!decimal.TryParse(MontoTexto, out var monto) || monto <= 0)
        {
            MensajeError = "Ingresá un monto válido, mayor a cero.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;

            var direccion = DireccionTexto == "Ingreso" ? DireccionMovimiento.Ingreso : DireccionMovimiento.Salida;
            var categoria = CategoriaTexto switch
            {
                "Pago del Personal" => CategoriaMovimientoCaja.PagoPersonal,
                "Ajuste de Caja" => CategoriaMovimientoCaja.AjusteCaja,
                "Consumo de Personal" => CategoriaMovimientoCaja.ConsumoPersonal,
                _ => CategoriaMovimientoCaja.GastosDiarios
            };
            var origen = OrigenTexto == "Sauna" ? OrigenCajaChica.Sauna : OrigenCajaChica.Hotel;

            await _gastosService.RegistrarMovimientoAsync(new NuevoMovimientoCajaDto
            {
                Direccion = direccion,
                Categoria = categoria,
                Descripcion = Descripcion.Trim(),
                PersonalRelacionado = string.IsNullOrWhiteSpace(PersonalRelacionado) ? null : PersonalRelacionado.Trim(),
                Monto = monto,
                OrigenCaja = origen,
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
