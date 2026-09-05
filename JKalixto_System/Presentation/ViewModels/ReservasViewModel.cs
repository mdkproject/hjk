using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class ReservasViewModel : BaseViewModel
{
    private readonly IReservaService _reservaService;
    private readonly IHabitacionService _habitacionService;
    private readonly ISessionService _sessionService;

    public ObservableCollection<ReservaCardDto> Reservas { get; } = new();

    private bool _hayReservas;
    public bool HayReservas
    {
        get => _hayReservas;
        set => SetProperty(ref _hayReservas, value);
    }

    /// <summary>Copia completa de las 36 habitaciones, para el panel de disponibilidad
    /// a la derecha — mismo patrón que RecepcionViewModel: se filtra en memoria.</summary>
    private List<HabitacionCardDto> _todasLasHabitaciones = new();

    public ObservableCollection<HabitacionCardDto> Habitaciones { get; } = new();

    /// <summary>Null = "Todos los pisos". Mismo patrón que RecepcionViewModel.</summary>
    private int? _pisoSeleccionado;
    public int? PisoSeleccionado
    {
        get => _pisoSeleccionado;
        set
        {
            if (SetProperty(ref _pisoSeleccionado, value))
            {
                OnPropertyChanged(nameof(PisoSeleccionadoTexto));
                AplicarFiltroHabitaciones();
            }
        }
    }

    /// <summary>Versión en texto de PisoSeleccionado, para resaltar el botón activo con
    /// un DataTrigger en el XAML (igual que en RecepcionViewModel).</summary>
    public string PisoSeleccionadoTexto => _pisoSeleccionado?.ToString() ?? "Todos";

    private int _contadorDisponible;
    public int ContadorDisponible
    {
        get => _contadorDisponible;
        set => SetProperty(ref _contadorDisponible, value);
    }

    private int _contadorOcupada;
    public int ContadorOcupada
    {
        get => _contadorOcupada;
        set => SetProperty(ref _contadorOcupada, value);
    }

    private int _contadorLimpieza;
    public int ContadorLimpieza
    {
        get => _contadorLimpieza;
        set => SetProperty(ref _contadorLimpieza, value);
    }

    private int _contadorMantenimiento;
    public int ContadorMantenimiento
    {
        get => _contadorMantenimiento;
        set => SetProperty(ref _contadorMantenimiento, value);
    }

    private string _filtroTipoTexto = "Todos";
    public string FiltroTipoTexto
    {
        get => _filtroTipoTexto;
        set
        {
            if (SetProperty(ref _filtroTipoTexto, value))
            {
                AplicarFiltroHabitaciones();
            }
        }
    }

    private string _filtroEstadoTexto = "Todos";
    public string FiltroEstadoTexto
    {
        get => _filtroEstadoTexto;
        set
        {
            if (SetProperty(ref _filtroEstadoTexto, value))
            {
                AplicarFiltroHabitaciones();
            }
        }
    }

    public ICommand NuevaReservaCommand { get; }
    public ICommand SeleccionarReservaCommand { get; }
    public ICommand SeleccionarPisoCommand { get; }
    public ICommand SeleccionarFiltroTipoCommand { get; }
    public ICommand SeleccionarFiltroEstadoCommand { get; }

    public ReservasViewModel(IReservaService reservaService, IHabitacionService habitacionService, ISessionService sessionService)
    {
        _reservaService = reservaService;
        _habitacionService = habitacionService;
        _sessionService = sessionService;
        Title = "Reservas";

        NuevaReservaCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(ReservaNuevaPage)));

        SeleccionarReservaCommand = new Command<ReservaCardDto>(async (reserva) =>
        {
            if (reserva is not null)
            {
                await GestionarReservaAsync(reserva);
            }
        });

        SeleccionarPisoCommand = new Command<string>((piso) =>
        {
            if (piso is null)
            {
                return;
            }

            PisoSeleccionado = piso == "Todos" ? null : int.Parse(piso);
        });

        SeleccionarFiltroTipoCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                FiltroTipoTexto = tipo;
            }
        });

        SeleccionarFiltroEstadoCommand = new Command<string>((estado) =>
        {
            if (estado is not null)
            {
                FiltroEstadoTexto = estado;
            }
        });
    }

    public async Task CargarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var lista = await _reservaService.ObtenerProximasAsync();

            Reservas.Clear();
            foreach (var r in lista)
            {
                Reservas.Add(r);
            }
            HayReservas = Reservas.Count > 0;

            _todasLasHabitaciones = await _habitacionService.ObtenerTodasAsync();
            AplicarFiltroHabitaciones();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AplicarFiltroHabitaciones()
    {
        IEnumerable<HabitacionCardDto> query = PisoSeleccionado.HasValue
            ? _todasLasHabitaciones.Where(h => h.Piso == PisoSeleccionado.Value)
            : _todasLasHabitaciones;

        if (FiltroTipoTexto != "Todos")
        {
            var tipo = FiltroTipoTexto switch
            {
                "Simple" => TipoHabitacion.Simple,
                "Matrimonial" => TipoHabitacion.Matrimonial,
                "Doble" => TipoHabitacion.Doble,
                "Familiar" => TipoHabitacion.Familiar,
                "Suite" => TipoHabitacion.Suite,
                _ => (TipoHabitacion?)null
            };
            if (tipo.HasValue)
            {
                query = query.Where(h => h.Tipo == tipo.Value);
            }
        }

        // Igual que en RecepcionViewModel: los contadores reflejan piso+tipo (no el
        // estado), para que siempre muestren un desglose útil sin importar en qué
        // filtro de estado estás parado.
        var paraContar = query.ToList();
        ContadorDisponible = paraContar.Count(h => h.Estado == EstadoHabitacion.Disponible);
        ContadorOcupada = paraContar.Count(h => h.Estado == EstadoHabitacion.Ocupada);
        ContadorLimpieza = paraContar.Count(h => h.Estado == EstadoHabitacion.LimpiezaSalida);
        ContadorMantenimiento = paraContar.Count(h => h.Estado == EstadoHabitacion.Mantenimiento);

        if (FiltroEstadoTexto != "Todos")
        {
            var estado = FiltroEstadoTexto switch
            {
                "Disponible" => EstadoHabitacion.Disponible,
                "Ocupada" => EstadoHabitacion.Ocupada,
                "Limpieza" => EstadoHabitacion.LimpiezaSalida,
                "Mantenimiento" => EstadoHabitacion.Mantenimiento,
                _ => (EstadoHabitacion?)null
            };
            if (estado.HasValue)
            {
                query = paraContar.Where(h => h.Estado == estado.Value);
            }
        }

        Habitaciones.Clear();
        foreach (var h in query.OrderBy(h => h.Numero))
        {
            Habitaciones.Add(h);
        }
    }

    private async Task GestionarReservaAsync(ReservaCardDto reserva)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        var opciones = new System.Collections.Generic.List<string>();
        if (reserva.PuedeConvertirACheckIn)
        {
            opciones.Add("Hacer Check-in ahora");
        }
        if (reserva.PuedeCancelar)
        {
            opciones.Add("Cancelar reserva");
        }

        if (opciones.Count == 0)
        {
            await page.DisplayAlertAsync(
                $"Reserva — {reserva.NombreCliente}",
                $"Habitación {reserva.NumeroHabitacion} ({reserva.EtiquetaTipoHabitacion})\n" +
                $"{reserva.RangoFechasTexto} · {reserva.Noches} noche(s)\n" +
                $"Estado: {reserva.EtiquetaEstado}\n" +
                $"Registrada el: {reserva.FechaCreacionTexto}",
                "Cerrar");
            return;
        }

        var accion = await page.DisplayActionSheetAsync(
            $"Hab. {reserva.NumeroHabitacion} — {reserva.NombreCliente}\n{reserva.RangoFechasTexto}",
            "Cancelar", null,
            opciones.ToArray());

        if (accion == "Hacer Check-in ahora")
        {
            var confirmar = await page.DisplayAlertAsync(
                "Confirmar Check-in",
                $"¿Registrar el Check-in de {reserva.NombreCliente} en la habitación {reserva.NumeroHabitacion} ahora mismo ({System.DateTime.Now:dd/MM/yyyy HH:mm})?",
                "Sí, hacer Check-in", "Cancelar");

            if (confirmar)
            {
                await EjecutarConManejoDeErroresAsync(page, async () =>
                {
                    var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
                    await _reservaService.ConvertirEnCheckInAsync(reserva.ReservaId, usuarioId);
                    await CargarAsync();
                });
            }
        }
        else if (accion == "Cancelar reserva")
        {
            var confirmar = await page.DisplayAlertAsync(
                "Cancelar reserva",
                $"¿Cancelar la reserva de {reserva.NombreCliente} para la habitación {reserva.NumeroHabitacion}?",
                "Sí, cancelar", "No");

            if (confirmar)
            {
                await EjecutarConManejoDeErroresAsync(page, async () =>
                {
                    var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
                    await _reservaService.CancelarReservaAsync(reserva.ReservaId, usuarioId);
                    await CargarAsync();
                });
            }
        }
    }

    private static async Task EjecutarConManejoDeErroresAsync(Page page, System.Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (System.InvalidOperationException ex)
        {
            await page.DisplayAlertAsync("No se pudo completar", ex.Message, "Entendido");
        }
        catch (System.Exception)
        {
            await page.DisplayAlertAsync("No se pudo completar", "Ocurrió un error inesperado. Intente nuevamente.", "Entendido");
        }
    }
}
