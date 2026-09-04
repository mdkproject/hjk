using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class GastosViewModel : BaseViewModel
{
    private readonly IGastosService _gastosService;
    private readonly ISessionService _sessionService;

    public ObservableCollection<MovimientoCajaCardDto> Movimientos { get; } = new();

    private bool _hayMovimientos;
    public bool HayMovimientos
    {
        get => _hayMovimientos;
        set => SetProperty(ref _hayMovimientos, value);
    }

    private string _cajaChicaHotelTexto = string.Empty;
    public string CajaChicaHotelTexto
    {
        get => _cajaChicaHotelTexto;
        set => SetProperty(ref _cajaChicaHotelTexto, value);
    }

    private string _cajaChicaSaunaTexto = string.Empty;
    public string CajaChicaSaunaTexto
    {
        get => _cajaChicaSaunaTexto;
        set => SetProperty(ref _cajaChicaSaunaTexto, value);
    }

    public ICommand NuevoMovimientoCommand { get; }
    public ICommand SeleccionarMovimientoCommand { get; }

    public GastosViewModel(IGastosService gastosService, ISessionService sessionService)
    {
        _gastosService = gastosService;
        _sessionService = sessionService;
        Title = "Gastos";

        NuevoMovimientoCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(GastoNuevoPage)));

        SeleccionarMovimientoCommand = new Command<MovimientoCajaCardDto>(async (movimiento) =>
        {
            if (movimiento is not null)
            {
                await GestionarMovimientoAsync(movimiento);
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
            var hoy = DateTime.Now;

            var lista = await _gastosService.ObtenerDelDiaAsync(hoy);
            Movimientos.Clear();
            foreach (var m in lista)
            {
                Movimientos.Add(m);
            }
            HayMovimientos = Movimientos.Count > 0;

            var cajaChica = await _gastosService.ObtenerResumenCajaChicaAsync(hoy);
            CajaChicaHotelTexto = $"S/ {cajaChica.MontoEsperadoHotel:0.00}  (base S/ {cajaChica.MontoBaseHotel:0.00})";
            CajaChicaSaunaTexto = $"S/ {cajaChica.MontoEsperadoSauna:0.00}  (base S/ {cajaChica.MontoBaseSauna:0.00})";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GestionarMovimientoAsync(MovimientoCajaCardDto movimiento)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        var confirmar = await page.DisplayAlertAsync(
            movimiento.TipoCompletoTexto,
            $"{movimiento.Descripcion}\nMonto: {movimiento.MontoTexto}\nRegistrado por: {movimiento.UsuarioNombre} a las {movimiento.HoraTexto}\n\n¿Eliminar este movimiento?",
            "Sí, eliminar", "Cerrar");

        if (confirmar)
        {
            try
            {
                var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
                await _gastosService.EliminarMovimientoAsync(movimiento.Id, usuarioId);
                await CargarAsync();
            }
            catch (System.InvalidOperationException ex)
            {
                await page.DisplayAlertAsync("No se pudo eliminar", ex.Message, "Entendido");
            }
        }
    }
}
