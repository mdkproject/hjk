using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class ReservaNuevaViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IReservaService _reservaService;
    private readonly ISessionService _sessionService;

    /// <summary>Copia completa de habitaciones libres para el rango de fechas, traída del
    /// servicio. El filtro de tipo se aplica en memoria sobre esta lista.</summary>
    private List<HabitacionDisponibleDto> _todasLasDisponibles = new();

    /// <summary>Habitación a preseleccionar la primera vez que se cargan las disponibles
    /// (llega por query string cuando la reserva se abre desde una celda del Calendario).</summary>
    private int? _habitacionIdPreseleccionada;

    private DateTime _fechaInicio = DateTime.Now.Date.AddDays(1);
    public DateTime FechaInicio
    {
        get => _fechaInicio;
        set
        {
            if (SetProperty(ref _fechaInicio, value))
            {
                _ = CargarDisponiblesAsync();
            }
        }
    }

    private DateTime _fechaFin = DateTime.Now.Date.AddDays(2);
    public DateTime FechaFin
    {
        get => _fechaFin;
        set
        {
            if (SetProperty(ref _fechaFin, value))
            {
                _ = CargarDisponiblesAsync();
            }
        }
    }

    /// <summary>Tipo de habitación elegido: la reserva SIEMPRE filtra por un tipo a la vez
    /// (en vez de mostrar las 5 categorías mezcladas en una sola cuadrícula grande).</summary>
    private string _tipoFiltro = "Simple";
    public string TipoFiltro
    {
        get => _tipoFiltro;
        set
        {
            if (SetProperty(ref _tipoFiltro, value))
            {
                AplicarFiltroTipo();
            }
        }
    }

    public ObservableCollection<HabitacionDisponibleDto> HabitacionesDisponibles { get; } = new();

    private HabitacionDisponibleDto? _habitacionSeleccionada;
    public HabitacionDisponibleDto? HabitacionSeleccionada
    {
        get => _habitacionSeleccionada;
        set
        {
            if (SetProperty(ref _habitacionSeleccionada, value))
            {
                OnPropertyChanged(nameof(HaySeleccion));
                OnPropertyChanged(nameof(NumeroSeleccionadoTexto));
            }
        }
    }

    public bool HaySeleccion => HabitacionSeleccionada is not null;

    public string NumeroSeleccionadoTexto => HabitacionSeleccionada is null
        ? string.Empty
        : $"Elegida: Habitación {HabitacionSeleccionada.Numero}";

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

    private string _observaciones = string.Empty;
    public string Observaciones
    {
        get => _observaciones;
        set => SetProperty(ref _observaciones, value);
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

    private bool _hayDisponibles;
    public bool HayDisponibles
    {
        get => _hayDisponibles;
        set => SetProperty(ref _hayDisponibles, value);
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
    public ICommand SeleccionarTipoFiltroCommand { get; }
    public ICommand AgregarAcompananteCommand { get; }
    public ICommand QuitarAcompananteCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public ReservaNuevaViewModel(IReservaService reservaService, ISessionService sessionService)
    {
        _reservaService = reservaService;
        _sessionService = sessionService;
        Title = "Nueva Reserva";

        SeleccionarTipoDocumentoCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                TipoDocumentoTexto = tipo;
            }
        });

        SeleccionarTipoFiltroCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                TipoFiltro = tipo;
            }
        });

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

        ConfirmarCommand = new Command(async () => await ConfirmarAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    /// <summary>Recibe habitacionId/fechaInicio/fechaFin cuando la reserva se abre desde
    /// una celda Disponible del Calendario (ver CalendarioPage.xaml.cs). Se aplican directo
    /// sobre los campos privados (sin pasar por los setters) para no disparar una carga
    /// duplicada de disponibles: CargarAsync ya la hace una sola vez en OnAppearing.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("fechaInicio", out var fiValue) && DateTime.TryParse(fiValue?.ToString(), out var fechaInicio))
        {
            _fechaInicio = fechaInicio.Date;
        }

        if (query.TryGetValue("fechaFin", out var ffValue) && DateTime.TryParse(ffValue?.ToString(), out var fechaFin))
        {
            _fechaFin = fechaFin.Date;
        }

        if (query.TryGetValue("habitacionId", out var idValue) && int.TryParse(idValue?.ToString(), out var habitacionId))
        {
            _habitacionIdPreseleccionada = habitacionId;
        }

        OnPropertyChanged(nameof(FechaInicio));
        OnPropertyChanged(nameof(FechaFin));
    }

    public async Task CargarAsync() => await CargarDisponiblesAsync();

    private async Task CargarDisponiblesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (FechaFin <= FechaInicio)
        {
            _todasLasDisponibles = new List<HabitacionDisponibleDto>();
            HabitacionesDisponibles.Clear();
            HayDisponibles = false;
            HabitacionSeleccionada = null;
            MensajeError = "La fecha de salida debe ser posterior a la de entrada.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;
            _todasLasDisponibles = await _reservaService.ObtenerHabitacionesDisponiblesAsync(FechaInicio, FechaFin);

            // Si venimos del Calendario con una habitación ya elegida, el filtro de tipo
            // salta directo al tipo de ESA habitación (para no obligar al usuario a
            // adivinar en qué pestaña de tipo está su propia habitación).
            if (_habitacionIdPreseleccionada.HasValue)
            {
                var preseleccionada = _todasLasDisponibles.FirstOrDefault(h => h.HabitacionId == _habitacionIdPreseleccionada.Value);
                _habitacionIdPreseleccionada = null;

                if (preseleccionada is not null)
                {
                    if (preseleccionada.EtiquetaTipo == TipoFiltro)
                    {
                        AplicarFiltroTipo();
                    }
                    else
                    {
                        TipoFiltro = preseleccionada.EtiquetaTipo; // dispara AplicarFiltroTipo
                    }

                    HabitacionSeleccionada = HabitacionesDisponibles.FirstOrDefault(h => h.HabitacionId == preseleccionada.HabitacionId);
                    return;
                }
            }

            AplicarFiltroTipo();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Filtra en memoria _todasLasDisponibles por TipoFiltro y vuelve a armar
    /// HabitacionesDisponibles — así cambiar de tipo es instantáneo y no golpea la BD.</summary>
    private void AplicarFiltroTipo()
    {
        var tipo = TipoFiltro switch
        {
            "Simple" => TipoHabitacion.Simple,
            "Matrimonial" => TipoHabitacion.Matrimonial,
            "Doble" => TipoHabitacion.Doble,
            "Familiar" => TipoHabitacion.Familiar,
            "Suite" => TipoHabitacion.Suite,
            _ => (TipoHabitacion?)null
        };

        var idPrevio = HabitacionSeleccionada?.HabitacionId;
        var filtradas = tipo.HasValue
            ? _todasLasDisponibles.Where(h => h.Tipo == tipo.Value)
            : _todasLasDisponibles;

        HabitacionesDisponibles.Clear();
        foreach (var h in filtradas)
        {
            HabitacionesDisponibles.Add(h);
        }
        HayDisponibles = HabitacionesDisponibles.Count > 0;

        // Si la habitación elegida sigue en la lista filtrada, la vuelve a seleccionar.
        HabitacionSeleccionada = idPrevio.HasValue
            ? HabitacionesDisponibles.FirstOrDefault(h => h.HabitacionId == idPrevio.Value)
            : null;
    }

    private async Task ConfirmarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (HabitacionSeleccionada is null)
        {
            MensajeError = "Elegí una habitación de la lista de disponibles.";
            HayError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(NumeroDocumento) || string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(Celular))
        {
            MensajeError = "El número de documento, nombre completo y celular son obligatorios.";
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
            var tipoDocumento = TipoDocumentoTexto switch
            {
                "Pasaporte" => TipoDocumento.Pasaporte,
                "Carné Ext." => TipoDocumento.CarneExtranjeria,
                _ => TipoDocumento.DNI
            };

            await _reservaService.CrearReservaAsync(new NuevaReservaDto
            {
                HabitacionId = HabitacionSeleccionada.HabitacionId,
                TipoDocumento = tipoDocumento,
                NumeroDocumento = NumeroDocumento.Trim(),
                NombreCompleto = NombreCompleto.Trim(),
                Celular = Celular.Trim(),
                FechaInicio = FechaInicio,
                FechaFin = FechaFin,
                Observaciones = string.IsNullOrWhiteSpace(Observaciones) ? null : Observaciones.Trim(),
                TipoComprobante = EsFactura ? TipoComprobante.Factura : TipoComprobante.Boleta,
                RUC = EsFactura ? RUC.Trim() : null,
                RazonSocial = EsFactura ? RazonSocial.Trim() : null,
                CorreoFacturacion = EsFactura ? CorreoFacturacion.Trim() : null,
                Acompanantes = Acompanantes.ToList(),
                UsuarioId = usuarioId
            });

            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            HayError = true;
        }
        catch (Exception)
        {
            MensajeError = "No se pudo crear la reserva. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
