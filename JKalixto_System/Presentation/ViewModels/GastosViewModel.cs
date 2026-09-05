using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

    private List<MovimientoCajaCardDto> _todosLosMovimientos = new();

    public ObservableCollection<MovimientoCajaCardDto> Movimientos { get; } = new();

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set
        {
            if (SetProperty(ref _textoBusqueda, value))
            {
                AplicarFiltroBusqueda();
            }
        }
    }

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

            _todosLosMovimientos = await _gastosService.ObtenerDelDiaAsync(hoy);
            AplicarFiltroBusqueda();

            var cajaChica = await _gastosService.ObtenerResumenCajaChicaAsync(hoy);
            CajaChicaHotelTexto = $"S/ {cajaChica.MontoEsperadoHotel:0.00}  (base S/ {cajaChica.MontoBaseHotel:0.00})";
            CajaChicaSaunaTexto = $"S/ {cajaChica.MontoEsperadoSauna:0.00}  (base S/ {cajaChica.MontoBaseSauna:0.00})";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Filtra los movimientos de HOY por descripción o personal relacionado —
    /// no golpea la base de datos, ya están todos cargados en memoria.</summary>
    private void AplicarFiltroBusqueda()
    {
        IEnumerable<MovimientoCajaCardDto> query = _todosLosMovimientos;

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(m =>
                m.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                (m.PersonalRelacionado?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Movimientos.Clear();
        foreach (var m in query)
        {
            Movimientos.Add(m);
        }
        HayMovimientos = Movimientos.Count > 0;
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
            catch (System.UnauthorizedAccessException ex)
            {
                await page.DisplayAlertAsync("Sin permiso", ex.Message, "Entendido");
            }
        }
    }
}
