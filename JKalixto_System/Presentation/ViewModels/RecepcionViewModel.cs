using System.Collections.ObjectModel;
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

    public ObservableCollection<HabitacionCardDto> Habitaciones { get; } = new();

    private int _pisoSeleccionado = 1;
    public int PisoSeleccionado
    {
        get => _pisoSeleccionado;
        set => SetProperty(ref _pisoSeleccionado, value);
    }

    private bool _hayHabitaciones;
    public bool HayHabitaciones
    {
        get => _hayHabitaciones;
        set => SetProperty(ref _hayHabitaciones, value);
    }

    public ICommand SeleccionarPisoCommand { get; }
    public ICommand SeleccionarHabitacionCommand { get; }

    public RecepcionViewModel(IHabitacionService habitacionService, ISessionService sessionService)
    {
        _habitacionService = habitacionService;
        _sessionService = sessionService;
        Title = "Recepción — Hotel";

        SeleccionarPisoCommand = new Command<string>(async (piso) =>
        {
            if (piso is not null && int.TryParse(piso, out var numeroPiso))
            {
                await CargarPisoAsync(numeroPiso);
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

    public async Task CargarAsync() => await CargarPisoAsync(PisoSeleccionado);

    private async Task CargarPisoAsync(int piso)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            PisoSeleccionado = piso;

            var lista = await _habitacionService.ObtenerPorPisoAsync(piso);

            Habitaciones.Clear();
            foreach (var h in lista)
            {
                Habitaciones.Add(h);
            }
            HayHabitaciones = Habitaciones.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
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
                    await CargarPisoAsync(PisoSeleccionado);
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
                    await CargarPisoAsync(PisoSeleccionado);
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
            await CargarPisoAsync(PisoSeleccionado);
        }
    }

    private async Task GestionarOcupadaAsync(Page page, HabitacionCardDto habitacion, int usuarioId)
    {
        var accion = await page.DisplayActionSheetAsync(
            $"Hab. {habitacion.Numero} — {habitacion.NombreHuesped}\nTotal acumulado: S/ {habitacion.TotalAcumulado:0.00}",
            "Cancelar", null,
            "Hacer Check-out", "Solicitar limpieza intermedia");

        if (accion == "Hacer Check-out")
        {
            var confirmar = await page.DisplayAlertAsync(
                "Confirmar Check-out",
                $"¿Cerrar la estadía de {habitacion.NombreHuesped}?\nTotal a cobrar: S/ {habitacion.TotalAcumulado:0.00}",
                "Sí, hacer Check-out", "Cancelar");

            if (confirmar && habitacion.EstadiaId.HasValue)
            {
                await _habitacionService.CheckOutAsync(habitacion.EstadiaId.Value, usuarioId);
                await CargarPisoAsync(PisoSeleccionado);
            }
        }
        else if (accion == "Solicitar limpieza intermedia")
        {
            await _habitacionService.RegistrarLimpiezaIntermediaAsync(habitacion.HabitacionId, usuarioId);
            await page.DisplayAlertAsync("Listo", "Se registró la solicitud de limpieza intermedia.", "OK");
        }
    }
}
