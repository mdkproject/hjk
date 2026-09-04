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

public class RecepcionViewModel : BaseViewModel
{
    private readonly IHabitacionService _habitacionService;
    private readonly ISessionService _sessionService;

    /// <summary>Copia completa (36 habitaciones) traída una sola vez de la BD. Los filtros
    /// (piso, tipo, estado) se aplican en memoria sobre esta lista — así cambiar de filtro
    /// es instantáneo y no golpea la base de datos cada vez.</summary>
    private List<HabitacionCardDto> _todasLasHabitaciones = new();

    public ObservableCollection<HabitacionCardDto> Habitaciones { get; } = new();

    /// <summary>Null = "Todos los pisos". Nullable para poder representar ese caso
    /// además de los pisos 1-4.</summary>
    private int? _pisoSeleccionado = 1;
    public int? PisoSeleccionado
    {
        get => _pisoSeleccionado;
        set
        {
            if (SetProperty(ref _pisoSeleccionado, value))
            {
                OnPropertyChanged(nameof(PisoSeleccionadoTexto));
            }
        }
    }

    /// <summary>Versión en texto de PisoSeleccionado ("Todos" o el número), solo para
    /// poder resaltar el botón activo con un DataTrigger en el XAML (que compara
    /// contra CommandParameter, que es string).</summary>
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
        set => SetProperty(ref _filtroTipoTexto, value);
    }

    private string _filtroEstadoTexto = "Todos";
    public string FiltroEstadoTexto
    {
        get => _filtroEstadoTexto;
        set => SetProperty(ref _filtroEstadoTexto, value);
    }

    private bool _hayHabitaciones;
    public bool HayHabitaciones
    {
        get => _hayHabitaciones;
        set => SetProperty(ref _hayHabitaciones, value);
    }

    public ICommand SeleccionarPisoCommand { get; }
    public ICommand SeleccionarFiltroTipoCommand { get; }
    public ICommand SeleccionarFiltroEstadoCommand { get; }
    public ICommand SeleccionarHabitacionCommand { get; }

    public RecepcionViewModel(IHabitacionService habitacionService, ISessionService sessionService)
    {
        _habitacionService = habitacionService;
        _sessionService = sessionService;
        Title = "Registro Hotel";

        SeleccionarPisoCommand = new Command<string>((piso) =>
        {
            if (piso is null)
            {
                return;
            }

            PisoSeleccionado = piso == "Todos" ? null : int.Parse(piso);
            AplicarFiltros();
        });

        SeleccionarFiltroTipoCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                FiltroTipoTexto = tipo;
                AplicarFiltros();
            }
        });

        SeleccionarFiltroEstadoCommand = new Command<string>((estado) =>
        {
            if (estado is not null)
            {
                FiltroEstadoTexto = estado;
                AplicarFiltros();
            }
        });

        SeleccionarHabitacionCommand = new Command<HabitacionCardDto>(async (habitacion) =>
        {
            if (habitacion is not null)
            {
                await GestionarHabitacionAsync(habitacion);
            }
        });
    }

    /// <summary>Punto de entrada público: trae las 36 habitaciones frescas de la BD y
    /// vuelve a aplicar los filtros actuales. Se llama al entrar a la pantalla y después
    /// de cualquier acción que cambie el estado de una habitación (check-in, checkout, etc).</summary>
    public async Task CargarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            _todasLasHabitaciones = await _habitacionService.ObtenerTodasAsync();
            AplicarFiltros();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AplicarFiltros()
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

        // Los contadores reflejan piso+tipo (no el estado) para que siempre muestren
        // un desglose útil, sin importar en qué pestaña de estado estás parado.
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
        HayHabitaciones = Habitaciones.Count > 0;
    }

    private async Task GestionarHabitacionAsync(HabitacionCardDto habitacion)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;

        switch (habitacion.Estado)
        {
            case EstadoHabitacion.Disponible:
                await GestionarDisponibleAsync(page, habitacion);
                break;

            case EstadoHabitacion.Ocupada:
                await GestionarOcupadaAsync(page, habitacion, usuarioId);
                break;

            case EstadoHabitacion.LimpiezaSalida:
                var confirmarLimpieza = await page.DisplayAlertAsync(
                    "Marcar como limpia",
                    $"¿La habitación {habitacion.Numero} ya está lista para un nuevo huésped?",
                    "Sí, está limpia", "Cancelar");
                if (confirmarLimpieza)
                {
                    await _habitacionService.FinalizarLimpiezaAsync(habitacion.HabitacionId);
                    await CargarAsync();
                }
                break;

            case EstadoHabitacion.Mantenimiento:
                var confirmarFinMant = await page.DisplayAlertAsync(
                    "Finalizar mantenimiento",
                    $"Motivo registrado: {habitacion.MotivoMantenimiento}\n\n¿Marcar la habitación {habitacion.Numero} como Disponible?",
                    "Sí, finalizar", "Cancelar");
                if (confirmarFinMant)
                {
                    await _habitacionService.FinalizarMantenimientoAsync(habitacion.HabitacionId, usuarioId);
                    await CargarAsync();
                }
                break;
        }
    }

    private async Task GestionarDisponibleAsync(Page page, HabitacionCardDto habitacion)
    {
        var accion = await page.DisplayActionSheetAsync(
            $"Habitación {habitacion.Numero} — {habitacion.EtiquetaTipo} (S/ {habitacion.TarifaNoche:0.00})",
            "Cancelar", null,
            "Hacer Check-in", "Enviar a Mantenimiento");

        if (accion == "Hacer Check-in")
        {
            await Shell.Current.GoToAsync($"{nameof(CheckInPage)}?habitacionId={habitacion.HabitacionId}&numero={habitacion.Numero}");
        }
        else if (accion == "Enviar a Mantenimiento")
        {
            var motivo = await page.DisplayPromptAsync(
                "Motivo de mantenimiento",
                "Este campo es obligatorio (regla anti-fraude): no se puede pasar a mantenimiento sin explicar por qué.",
                "Confirmar", "Cancelar",
                placeholder: "Ej: Aire acondicionado no enfría");

            if (motivo is null)
            {
                return; // el usuario canceló
            }

            if (string.IsNullOrWhiteSpace(motivo))
            {
                await page.DisplayAlertAsync("No se pudo continuar", "El motivo es obligatorio para pasar a Mantenimiento.", "Entendido");
                return;
            }

            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
            await _habitacionService.IniciarMantenimientoAsync(habitacion.HabitacionId, motivo, usuarioId);
            await CargarAsync();
        }
    }

    private async Task GestionarOcupadaAsync(Page page, HabitacionCardDto habitacion, int usuarioId)
    {
        var accion = await page.DisplayActionSheetAsync(
            $"Hab. {habitacion.Numero} — {habitacion.NombreHuesped}\nTotal acumulado: S/ {habitacion.TotalAcumulado:0.00}",
            "Cancelar", null,
            "Ver detalles del huésped", "Hacer Check-out", "Solicitar limpieza intermedia");

        if (accion == "Ver detalles del huésped")
        {
            var fechaTexto = habitacion.FechaCheckInHuesped?.ToString("dd/MM/yyyy HH:mm") ?? "-";
            await page.DisplayAlertAsync(
                $"Huésped — Hab. {habitacion.Numero}",
                $"Nombre: {habitacion.NombreHuesped}\n" +
                $"{habitacion.EtiquetaTipoDocumentoHuesped}: {habitacion.NumeroDocumentoHuesped}\n" +
                $"Celular: {habitacion.CelularHuesped}\n" +
                $"Check-in: {fechaTexto}\n" +
                $"Acompañantes: {habitacion.AcompanantesTexto}\n" +
                $"Total acumulado: S/ {habitacion.TotalAcumulado:0.00}",
                "Cerrar");
        }
        else if (accion == "Hacer Check-out")
        {
            var confirmar = await page.DisplayAlertAsync(
                "Confirmar Check-out",
                $"¿Cerrar la estadía de {habitacion.NombreHuesped}?\nTotal a cobrar: S/ {habitacion.TotalAcumulado:0.00}",
                "Sí, hacer Check-out", "Cancelar");

            if (confirmar && habitacion.EstadiaId.HasValue)
            {
                await _habitacionService.CheckOutAsync(habitacion.EstadiaId.Value, usuarioId);
                await CargarAsync();
            }
        }
        else if (accion == "Solicitar limpieza intermedia")
        {
            await _habitacionService.RegistrarLimpiezaIntermediaAsync(habitacion.HabitacionId, usuarioId);
            await page.DisplayAlertAsync("Listo", "Se registró la solicitud de limpieza intermedia.", "OK");
        }
    }
}
