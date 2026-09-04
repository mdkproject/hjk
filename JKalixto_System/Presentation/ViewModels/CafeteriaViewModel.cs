using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>
/// Punto de entrada único para vender Cafetería/servicios adicionales a un huésped de
/// Hotel o a un cliente de Sauna, con un switch entre los dos — sin que un huésped de
/// hotel necesite tener un registro de ClienteSauna solo para pedir un café. El
/// carrito/catálogo en sí se reutiliza de SaunaVentaPage (ver
/// SaunaVentaViewModel.EstadiaIdDirecta), para no duplicar esa lógica ya probada.
/// </summary>
public class CafeteriaViewModel : BaseViewModel
{
    private readonly ISaunaService _saunaService;

    public ObservableCollection<HabitacionCardDto> HuespedesActivos { get; } = new();
    public ObservableCollection<ClienteSaunaCardDto> ClientesSaunaActivos { get; } = new();

    private string _origenSeleccionado = "Hotel";
    public string OrigenSeleccionado
    {
        get => _origenSeleccionado;
        set => SetProperty(ref _origenSeleccionado, value);
    }

    private bool _hayCuentas;
    public bool HayCuentas
    {
        get => _hayCuentas;
        set => SetProperty(ref _hayCuentas, value);
    }

    public ICommand SeleccionarOrigenCommand { get; }
    public ICommand SeleccionarHuespedCommand { get; }
    public ICommand SeleccionarClienteSaunaCommand { get; }

    public CafeteriaViewModel(ISaunaService saunaService)
    {
        _saunaService = saunaService;
        Title = "Cafetería";

        SeleccionarOrigenCommand = new Command<string>((origen) =>
        {
            if (origen is not null && origen != OrigenSeleccionado)
            {
                OrigenSeleccionado = origen;
                ActualizarHayCuentas();
            }
        });

        SeleccionarHuespedCommand = new Command<HabitacionCardDto>(async (huesped) =>
        {
            if (huesped is null || huesped.EstadiaId is null)
            {
                return;
            }

            var nombreCodificado = Uri.EscapeDataString(huesped.NombreHuesped ?? string.Empty);
            await Shell.Current.GoToAsync(
                $"{nameof(SaunaVentaPage)}?estadiaId={huesped.EstadiaId}&numero={huesped.Numero}&nombre={nombreCodificado}");
        });

        SeleccionarClienteSaunaCommand = new Command<ClienteSaunaCardDto>(async (cliente) =>
        {
            if (cliente is null)
            {
                return;
            }

            var nombreCodificado = Uri.EscapeDataString(cliente.NombreCompleto);
            var esHuesped = cliente.EsHuespedHotel ? "1" : "0";
            await Shell.Current.GoToAsync(
                $"{nameof(SaunaVentaPage)}?clienteSaunaId={cliente.ClienteSaunaId}&nombre={nombreCodificado}&esHuespedHotel={esHuesped}");
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

            var huespedes = await _saunaService.BuscarHuespedesActivosAsync();
            HuespedesActivos.Clear();
            foreach (var h in huespedes)
            {
                HuespedesActivos.Add(h);
            }

            var clientesSauna = await _saunaService.ObtenerClientesActivosAsync();
            ClientesSaunaActivos.Clear();
            foreach (var c in clientesSauna)
            {
                ClientesSaunaActivos.Add(c);
            }

            ActualizarHayCuentas();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ActualizarHayCuentas()
    {
        HayCuentas = OrigenSeleccionado == "Sauna"
            ? ClientesSaunaActivos.Count > 0
            : HuespedesActivos.Count > 0;
    }
}
