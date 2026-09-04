using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class SaunaViewModel : BaseViewModel
{
    private readonly ISaunaService _saunaService;
    private readonly ISessionService _sessionService;

    public ObservableCollection<ClienteSaunaCardDto> ClientesActivos { get; } = new();

    private bool _hayClientes;
    public bool HayClientes
    {
        get => _hayClientes;
        set => SetProperty(ref _hayClientes, value);
    }

    public ICommand NuevoClienteCommand { get; }
    public ICommand SeleccionarClienteCommand { get; }

    public SaunaViewModel(ISaunaService saunaService, ISessionService sessionService)
    {
        _saunaService = saunaService;
        _sessionService = sessionService;
        Title = "Registro Sauna";

        NuevoClienteCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(SaunaRegistroPage)));

        SeleccionarClienteCommand = new Command<ClienteSaunaCardDto>(async (cliente) =>
        {
            if (cliente is not null)
            {
                await GestionarClienteAsync(cliente);
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
            var lista = await _saunaService.ObtenerClientesActivosAsync();

            ClientesActivos.Clear();
            foreach (var c in lista)
            {
                ClientesActivos.Add(c);
            }
            HayClientes = ClientesActivos.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GestionarClienteAsync(ClienteSaunaCardDto cliente)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        var accion = await page.DisplayActionSheetAsync(
            $"{cliente.NombreCompleto} — Candado {cliente.NumeroCandado}\nConsumo: S/ {cliente.TotalConsumo:0.00}",
            "Cancelar", null,
            "Vender productos (POS)", "Finalizar sesión de Sauna");

        if (accion == "Vender productos (POS)")
        {
            var esHuesped = cliente.EsHuespedHotel ? "1" : "0";
            await Shell.Current.GoToAsync($"{nameof(SaunaVentaPage)}?clienteSaunaId={cliente.ClienteSaunaId}&nombre={System.Uri.EscapeDataString(cliente.NombreCompleto)}&esHuespedHotel={esHuesped}");
        }
        else if (accion == "Finalizar sesión de Sauna")
        {
            var confirmar = await page.DisplayAlertAsync(
                "Finalizar sesión",
                $"¿Cerrar la sesión de Sauna de {cliente.NombreCompleto}?",
                "Sí, finalizar", "Cancelar");

            if (confirmar)
            {
                var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
                await _saunaService.FinalizarSesionAsync(cliente.ClienteSaunaId, usuarioId);
                await CargarAsync();
            }
        }
    }
}
