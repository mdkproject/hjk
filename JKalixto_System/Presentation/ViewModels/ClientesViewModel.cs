using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class ClientesViewModel : BaseViewModel
{
    private readonly IClientesService _clientesService;
    private readonly IHabitacionService _habitacionService;
    private readonly ISaunaService _saunaService;
    private readonly ISessionService _sessionService;

    public ObservableCollection<ClienteUnificadoDto> Clientes { get; } = new();

    private string _origenSeleccionado = "Hotel";
    public string OrigenSeleccionado
    {
        get => _origenSeleccionado;
        set => SetProperty(ref _origenSeleccionado, value);
    }

    private ClienteUnificadoDto? _clienteSeleccionado;
    public ClienteUnificadoDto? ClienteSeleccionado
    {
        get => _clienteSeleccionado;
        set => SetProperty(ref _clienteSeleccionado, value);
    }

    private DetalleClienteDto? _detalle;
    public DetalleClienteDto? Detalle
    {
        get => _detalle;
        set
        {
            if (SetProperty(ref _detalle, value))
            {
                OnPropertyChanged(nameof(HayDetalle));
                OnPropertyChanged(nameof(NoHayDetalle));
            }
        }
    }

    public bool HayDetalle => Detalle is not null;
    public bool NoHayDetalle => Detalle is null;

    private bool _hayClientes;
    public bool HayClientes
    {
        get => _hayClientes;
        set => SetProperty(ref _hayClientes, value);
    }

    public ICommand SeleccionarOrigenCommand { get; }
    public ICommand SeleccionarClienteCommand { get; }
    public ICommand AgregarConsumoCommand { get; }
    public ICommand PagarCommand { get; }

    public ClientesViewModel(
        IClientesService clientesService,
        IHabitacionService habitacionService,
        ISaunaService saunaService,
        ISessionService sessionService)
    {
        _clientesService = clientesService;
        _habitacionService = habitacionService;
        _saunaService = saunaService;
        _sessionService = sessionService;
        Title = "Clientes";

        SeleccionarOrigenCommand = new Command<string>(async (origen) =>
        {
            if (origen is not null && origen != OrigenSeleccionado)
            {
                OrigenSeleccionado = origen;
                Detalle = null;
                ClienteSeleccionado = null;
                await CargarListaAsync();
            }
        });

        SeleccionarClienteCommand = new Command<ClienteUnificadoDto>(async (cliente) =>
        {
            if (cliente is not null)
            {
                ClienteSeleccionado = cliente;
                await CargarDetalleAsync(cliente);
            }
        });

        AgregarConsumoCommand = new Command(async () =>
        {
            if (Detalle is null)
            {
                return;
            }

            var nombreCodificado = Uri.EscapeDataString(Detalle.NombreParaVenta ?? string.Empty);

            if (Detalle.ClienteSaunaIdParaVenta is int clienteSaunaId)
            {
                var esHuesped = Detalle.EsHuespedHotelParaVenta ? "1" : "0";
                await Shell.Current.GoToAsync(
                    $"{nameof(SaunaVentaPage)}?clienteSaunaId={clienteSaunaId}&nombre={nombreCodificado}&esHuespedHotel={esHuesped}");
            }
            else if (Detalle.EstadiaIdParaVenta is int estadiaId)
            {
                await Shell.Current.GoToAsync(
                    $"{nameof(SaunaVentaPage)}?estadiaId={estadiaId}&numero={Detalle.NumeroHabitacionParaVenta}&nombre={nombreCodificado}");
            }
        });

        PagarCommand = new Command(async () => await PagarAsync());
    }

    public async Task CargarAsync()
    {
        await CargarListaAsync();

        // Si había un cliente seleccionado, refresca su detalle también
        // (por ejemplo, después de volver de "Agregar Consumo").
        if (ClienteSeleccionado is not null)
        {
            await CargarDetalleAsync(ClienteSeleccionado);
        }
    }

    private async Task CargarListaAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var lista = OrigenSeleccionado == "Sauna"
                ? await _clientesService.ObtenerClientesSaunaAsync()
                : await _clientesService.ObtenerClientesHotelAsync();

            Clientes.Clear();
            foreach (var c in lista)
            {
                Clientes.Add(c);
            }
            HayClientes = Clientes.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CargarDetalleAsync(ClienteUnificadoDto cliente)
    {
        Detalle = cliente.Origen == "Sauna" && cliente.ClienteSaunaId.HasValue
            ? await _clientesService.ObtenerDetalleSaunaAsync(cliente.ClienteSaunaId.Value)
            : cliente.EstadiaId.HasValue
                ? await _clientesService.ObtenerDetalleHotelAsync(cliente.EstadiaId.Value)
                : null;
    }

    /// <summary>Paga y finaliza directo desde Clientes — Check-out para huéspedes de
    /// hotel, o Finalizar sesión para clientes de sauna. Mismas acciones que ya existían
    /// en Registro Hotel / Registro Sauna, ahora también accesibles desde acá.</summary>
    private async Task PagarAsync()
    {
        if (Detalle is null || IsBusy)
        {
            return;
        }

        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;

        if (Detalle.EstadiaIdParaVenta is int estadiaId)
        {
            var confirmar = await page.DisplayAlertAsync(
                "Confirmar Check-out",
                $"¿Cerrar la estadía de {Detalle.NombreCliente}?\nTotal a cobrar: S/ {Detalle.Total:0.00}",
                "Sí, hacer Check-out", "Cancelar");

            if (!confirmar)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _habitacionService.CheckOutAsync(estadiaId, usuarioId);
            }
            finally
            {
                IsBusy = false;
            }
        }
        else if (Detalle.ClienteSaunaIdParaVenta is int clienteSaunaId)
        {
            var confirmar = await page.DisplayAlertAsync(
                "Pagar y finalizar sesión",
                $"¿Cobrar S/ {Detalle.Total:0.00} y cerrar la sesión de Sauna de {Detalle.NombreCliente}?",
                "Sí, pagar y finalizar", "Cancelar");

            if (!confirmar)
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _saunaService.FinalizarSesionAsync(clienteSaunaId, usuarioId);
            }
            finally
            {
                IsBusy = false;
            }
        }
        else
        {
            return;
        }

        await CargarListaAsync();
        if (ClienteSeleccionado is not null)
        {
            await CargarDetalleAsync(ClienteSeleccionado);
        }
    }
}
