using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class ReclamoNuevoViewModel : BaseViewModel
{
    private readonly IReclamosService _reclamosService;
    private readonly ISessionService _sessionService;

    // --- Datos del consumidor ---
    private string _nombreCompleto = string.Empty;
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    private string _domicilio = string.Empty;
    public string Domicilio
    {
        get => _domicilio;
        set => SetProperty(ref _domicilio, value);
    }

    private string _tipoDocumentoTexto = "DNI";
    public string TipoDocumentoTexto
    {
        get => _tipoDocumentoTexto;
        set => SetProperty(ref _tipoDocumentoTexto, value);
    }

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private string _telefono = string.Empty;
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    // --- Si es menor de edad ---
    private bool _esMenorDeEdad;
    public bool EsMenorDeEdad
    {
        get => _esMenorDeEdad;
        set => SetProperty(ref _esMenorDeEdad, value);
    }

    private string _nombreApoderado = string.Empty;
    public string NombreApoderado
    {
        get => _nombreApoderado;
        set => SetProperty(ref _nombreApoderado, value);
    }

    private string _documentoApoderado = string.Empty;
    public string DocumentoApoderado
    {
        get => _documentoApoderado;
        set => SetProperty(ref _documentoApoderado, value);
    }

    // --- El reclamo/queja en sí ---
    private string _tipoTexto = "Reclamo";
    public string TipoTexto
    {
        get => _tipoTexto;
        set => SetProperty(ref _tipoTexto, value);
    }

    private string _bienContratado = string.Empty;
    public string BienContratado
    {
        get => _bienContratado;
        set => SetProperty(ref _bienContratado, value);
    }

    private string _montoReclamadoTexto = string.Empty;
    public string MontoReclamadoTexto
    {
        get => _montoReclamadoTexto;
        set => SetProperty(ref _montoReclamadoTexto, value);
    }

    private string _detalleReclamo = string.Empty;
    public string DetalleReclamo
    {
        get => _detalleReclamo;
        set => SetProperty(ref _detalleReclamo, value);
    }

    private string _pedidoConsumidor = string.Empty;
    public string PedidoConsumidor
    {
        get => _pedidoConsumidor;
        set => SetProperty(ref _pedidoConsumidor, value);
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

    public ICommand SeleccionarTipoDocumentoCommand { get; }
    public ICommand SeleccionarTipoCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public ReclamoNuevoViewModel(IReclamosService reclamosService, ISessionService sessionService)
    {
        _reclamosService = reclamosService;
        _sessionService = sessionService;
        Title = "Nuevo Reclamo / Queja";

        SeleccionarTipoDocumentoCommand = new Command<string>((valor) =>
        {
            if (valor is not null)
            {
                TipoDocumentoTexto = valor;
            }
        });

        SeleccionarTipoCommand = new Command<string>((valor) =>
        {
            if (valor is not null)
            {
                TipoTexto = valor;
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

        if (string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(Domicilio) || string.IsNullOrWhiteSpace(NumeroDocumento))
        {
            MensajeError = "Nombre, domicilio y documento del consumidor son obligatorios.";
            HayError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(BienContratado))
        {
            MensajeError = "Indica qué producto o servicio contrató el consumidor.";
            HayError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(DetalleReclamo))
        {
            MensajeError = "El detalle del reclamo o queja es obligatorio.";
            HayError = true;
            return;
        }

        decimal? montoReclamado = null;
        if (!string.IsNullOrWhiteSpace(MontoReclamadoTexto))
        {
            if (!decimal.TryParse(MontoReclamadoTexto, out var monto) || monto < 0)
            {
                MensajeError = "El monto reclamado no es válido.";
                HayError = true;
                return;
            }
            montoReclamado = monto;
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;

            var tipoDocumento = TipoDocumentoTexto switch
            {
                "Pasaporte" => TipoDocumento.Pasaporte,
                "Carné Ext." => TipoDocumento.CarneExtranjeria,
                _ => TipoDocumento.DNI
            };

            var tipo = TipoTexto == "Queja" ? TipoReclamoQueja.Queja : TipoReclamoQueja.Reclamo;

            await _reclamosService.RegistrarAsync(new NuevoReclamoDto
            {
                NombreCompleto = NombreCompleto.Trim(),
                Domicilio = Domicilio.Trim(),
                TipoDocumento = tipoDocumento,
                NumeroDocumento = NumeroDocumento.Trim(),
                Telefono = string.IsNullOrWhiteSpace(Telefono) ? null : Telefono.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                EsMenorDeEdad = EsMenorDeEdad,
                NombreApoderado = EsMenorDeEdad && !string.IsNullOrWhiteSpace(NombreApoderado) ? NombreApoderado.Trim() : null,
                DocumentoApoderado = EsMenorDeEdad && !string.IsNullOrWhiteSpace(DocumentoApoderado) ? DocumentoApoderado.Trim() : null,
                BienContratado = BienContratado.Trim(),
                MontoReclamado = montoReclamado,
                Tipo = tipo,
                DetalleReclamo = DetalleReclamo.Trim(),
                PedidoConsumidor = string.IsNullOrWhiteSpace(PedidoConsumidor) ? null : PedidoConsumidor.Trim()
            }, usuarioId);

            var page = Shell.Current?.CurrentPage;
            if (page is not null)
            {
                await page.DisplayAlertAsync(
                    "Registrado",
                    "El establecimiento tiene 15 días hábiles (por ley) para responder este reclamo/queja.",
                    "OK");
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (System.InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            HayError = true;
        }
        catch (System.Exception)
        {
            MensajeError = "No se pudo registrar. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
