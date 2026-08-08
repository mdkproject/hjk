using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class CheckInViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IHabitacionService _habitacionService;
    private readonly ISessionService _sessionService;

    public int HabitacionId { get; private set; }

    private int _numeroHabitacion;
    public int NumeroHabitacion
    {
        get => _numeroHabitacion;
        set => SetProperty(ref _numeroHabitacion, value);
    }

    private string _dni = string.Empty;
    public string DNI
    {
        get => _dni;
        set => SetProperty(ref _dni, value);
    }

    private string _nombreCompleto = string.Empty;
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    private string _celular = string.Empty;
    public string Celular
    {
        get => _celular;
        set => SetProperty(ref _celular, value);
    }

    private bool _esFactura;
    public bool EsFactura
    {
        get => _esFactura;
        set => SetProperty(ref _esFactura, value);
    }

    private string _ruc = string.Empty;
    public string RUC
    {
        get => _ruc;
        set => SetProperty(ref _ruc, value);
    }

    private string _razonSocial = string.Empty;
    public string RazonSocial
    {
        get => _razonSocial;
        set => SetProperty(ref _razonSocial, value);
    }

    private string _correoFacturacion = string.Empty;
    public string CorreoFacturacion
    {
        get => _correoFacturacion;
        set => SetProperty(ref _correoFacturacion, value);
    }

    public ObservableCollection<string> Acompanantes { get; } = new();

    private string _nuevoAcompanante = string.Empty;
    public string NuevoAcompanante
    {
        get => _nuevoAcompanante;
        set => SetProperty(ref _nuevoAcompanante, value);
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

    public ICommand AgregarAcompananteCommand { get; }
    public ICommand QuitarAcompananteCommand { get; }
    public ICommand ConfirmarCheckInCommand { get; }
    public ICommand CancelarCommand { get; }

    public CheckInViewModel(IHabitacionService habitacionService, ISessionService sessionService)
    {
        _habitacionService = habitacionService;
        _sessionService = sessionService;
        Title = "Check-in";

        AgregarAcompananteCommand = new Command(() =>
        {
            if (!string.IsNullOrWhiteSpace(NuevoAcompanante))
            {
                Acompanantes.Add(NuevoAcompanante.Trim());
                NuevoAcompanante = string.Empty;
            }
        });

        QuitarAcompananteCommand = new Command<string>((nombre) =>
        {
            if (nombre is not null)
            {
                Acompanantes.Remove(nombre);
            }
        });

        ConfirmarCheckInCommand = new Command(async () => await ConfirmarAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    /// <summary>Recibe habitacionId y numero desde la URL de navegación (ver RecepcionViewModel).</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("habitacionId", out var idValue) && int.TryParse(idValue?.ToString(), out var id))
        {
            HabitacionId = id;
        }

        if (query.TryGetValue("numero", out var numValue) && int.TryParse(numValue?.ToString(), out var numero))
        {
            NumeroHabitacion = numero;
            Title = $"Check-in — Hab. {numero}";
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

        if (string.IsNullOrWhiteSpace(DNI) || string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(Celular))
        {
            MensajeError = "DNI, nombre completo y celular son obligatorios.";
            HayError = true;
            return;
        }

        if (EsFactura)
        {
            var rucLimpio = RUC.Trim();
            if (rucLimpio.Length != 11 || !rucLimpio.All(char.IsDigit))
            {
                MensajeError = "El RUC debe tener exactamente 11 dígitos.";
                HayError = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(RazonSocial) || string.IsNullOrWhiteSpace(CorreoFacturacion))
            {
                MensajeError = "Para Factura, la Razón Social y el Correo son obligatorios.";
                HayError = true;
                return;
            }
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;

            await _habitacionService.CheckInAsync(new NuevoCheckInDto
            {
                HabitacionId = HabitacionId,
                DNI = DNI.Trim(),
                NombreCompleto = NombreCompleto.Trim(),
                Celular = Celular.Trim(),
                TipoComprobante = EsFactura ? TipoComprobante.Factura : TipoComprobante.Boleta,
                RUC = EsFactura ? RUC.Trim() : null,
                RazonSocial = EsFactura ? RazonSocial.Trim() : null,
                CorreoFacturacion = EsFactura ? CorreoFacturacion.Trim() : null,
                Acompanantes = Acompanantes.ToList(),
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
            MensajeError = "No se pudo completar el Check-in. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
