using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class CierreCajaViewModel : BaseViewModel
{
    private readonly ICierreCajaService _cierreCajaService;
    private readonly ISessionService _sessionService;

    private string _totalHotelTexto = "S/ 0.00";
    public string TotalHotelTexto
    {
        get => _totalHotelTexto;
        set => SetProperty(ref _totalHotelTexto, value);
    }

    private string _totalSaunaTexto = "S/ 0.00";
    public string TotalSaunaTexto
    {
        get => _totalSaunaTexto;
        set => SetProperty(ref _totalSaunaTexto, value);
    }

    private string _totalGeneralTexto = "S/ 0.00";
    public string TotalGeneralTexto
    {
        get => _totalGeneralTexto;
        set => SetProperty(ref _totalGeneralTexto, value);
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

    private bool _puedeCerrar;
    public bool PuedeCerrar
    {
        get => _puedeCerrar;
        set => SetProperty(ref _puedeCerrar, value);
    }

    private bool _hayBloqueo;
    public bool HayBloqueo
    {
        get => _hayBloqueo;
        set => SetProperty(ref _hayBloqueo, value);
    }

    private string _mensajeBloqueo = string.Empty;
    public string MensajeBloqueo
    {
        get => _mensajeBloqueo;
        set => SetProperty(ref _mensajeBloqueo, value);
    }

    private string _turnoTexto = "Mañana";
    public string TurnoTexto
    {
        get => _turnoTexto;
        set => SetProperty(ref _turnoTexto, value);
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

    private bool _cierreCompletado;
    public bool CierreCompletado
    {
        get => _cierreCompletado;
        set => SetProperty(ref _cierreCompletado, value);
    }

    private string _horaCierreTexto = string.Empty;
    public string HoraCierreTexto
    {
        get => _horaCierreTexto;
        set => SetProperty(ref _horaCierreTexto, value);
    }

    public ICommand SeleccionarTurnoCommand { get; }
    public ICommand CerrarCajaCommand { get; }

    public CierreCajaViewModel(ICierreCajaService cierreCajaService, ISessionService sessionService)
    {
        _cierreCajaService = cierreCajaService;
        _sessionService = sessionService;
        Title = "Cierre de Caja";

        SeleccionarTurnoCommand = new Command<string>((turno) =>
        {
            if (turno is not null)
            {
                TurnoTexto = turno;
            }
        });

        CerrarCajaCommand = new Command(async () => await CerrarCajaAsync());
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
            CierreCompletado = false;
            var resumen = await _cierreCajaService.ObtenerResumenDelDiaAsync();

            TotalHotelTexto = $"S/ {resumen.TotalHotel:0.00}";
            TotalSaunaTexto = $"S/ {resumen.TotalSauna:0.00}";
            TotalGeneralTexto = $"S/ {resumen.TotalGeneral:0.00}";
            CajaChicaHotelTexto = $"S/ {resumen.MontoEsperadoCajaChicaHotel:0.00}  (base S/ {resumen.MontoBaseCajaChicaHotel:0.00})";
            CajaChicaSaunaTexto = $"S/ {resumen.MontoEsperadoCajaChicaSauna:0.00}  (base S/ {resumen.MontoBaseCajaChicaSauna:0.00})";
            PuedeCerrar = resumen.PuedeCerrar;
            HayBloqueo = !resumen.PuedeCerrar;

            MensajeBloqueo = resumen.PuedeCerrar
                ? string.Empty
                : $"No se puede cerrar caja: hay {resumen.ClientesSaunaAbiertos} cliente(s) de Sauna con sesión abierta ({string.Join(", ", resumen.NombresClientesAbiertos)}). Finalízalas desde el módulo de Sauna primero.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CerrarCajaAsync()
    {
        if (IsBusy || !PuedeCerrar)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
            var turno = TurnoTexto switch
            {
                "Tarde" => TurnoCaja.Tarde,
                "Noche" => TurnoCaja.Noche,
                _ => TurnoCaja.Manana
            };

            await _cierreCajaService.CerrarCajaAsync(turno, usuarioId);
            HoraCierreTexto = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            CierreCompletado = true;
        }
        catch (System.InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            HayError = true;
        }
        catch (System.Exception)
        {
            MensajeError = "No se pudo cerrar la caja. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
