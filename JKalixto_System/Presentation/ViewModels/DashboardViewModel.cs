using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>
/// Panel gerencial con KPIs en tiempo real. Los datos financieros se ocultan
/// (muestran "***") cuando el usuario conectado es Recepcionista.
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly ISessionService _sessionService;
    private readonly IAuditoriaService _auditoriaService;

    private string _nombreUsuario = string.Empty;
    public string NombreUsuario
    {
        get => _nombreUsuario;
        set => SetProperty(ref _nombreUsuario, value);
    }

    private string _rolUsuarioTexto = string.Empty;
    public string RolUsuarioTexto
    {
        get => _rolUsuarioTexto;
        set => SetProperty(ref _rolUsuarioTexto, value);
    }

    private bool _puedeVerFinanzas;
    /// <summary>Falso para Recepcionista: oculta ingresos y muestra "***" en su lugar.</summary>
    public bool PuedeVerFinanzas
    {
        get => _puedeVerFinanzas;
        set => SetProperty(ref _puedeVerFinanzas, value);
    }

    private string _ingresosHotelTexto = "S/ 0.00";
    public string IngresosHotelTexto
    {
        get => _ingresosHotelTexto;
        set => SetProperty(ref _ingresosHotelTexto, value);
    }

    private string _ingresosSaunaTexto = "S/ 0.00";
    public string IngresosSaunaTexto
    {
        get => _ingresosSaunaTexto;
        set => SetProperty(ref _ingresosSaunaTexto, value);
    }

    private string _ingresosTotalTexto = "S/ 0.00";
    public string IngresosTotalTexto
    {
        get => _ingresosTotalTexto;
        set => SetProperty(ref _ingresosTotalTexto, value);
    }

    private string _tasaOcupacionTexto = "0%";
    public string TasaOcupacionTexto
    {
        get => _tasaOcupacionTexto;
        set => SetProperty(ref _tasaOcupacionTexto, value);
    }

    /// <summary>ADR: tarifa promedio de las habitaciones ocupadas ahora mismo.</summary>
    private string _tarifaPromedioTexto = "S/ 0.00";
    public string TarifaPromedioTexto
    {
        get => _tarifaPromedioTexto;
        set => SetProperty(ref _tarifaPromedioTexto, value);
    }

    /// <summary>RevPAR: ingreso por habitación disponible (ADR × Ocupación).</summary>
    private string _revParTexto = "S/ 0.00";
    public string RevParTexto
    {
        get => _revParTexto;
        set => SetProperty(ref _revParTexto, value);
    }

    private int _clientesSaunaHoy;
    public int ClientesSaunaHoy
    {
        get => _clientesSaunaHoy;
        set => SetProperty(ref _clientesSaunaHoy, value);
    }

    private int _alertasActivas;
    public int AlertasActivas
    {
        get => _alertasActivas;
        set => SetProperty(ref _alertasActivas, value);
    }

    private bool _hayAlertas;
    public bool HayAlertas
    {
        get => _hayAlertas;
        set => SetProperty(ref _hayAlertas, value);
    }

    public ObservableCollection<EstadoHabitacionResumenDto> ResumenEstados { get; } = new();
    public ObservableCollection<LogAuditoria> UltimosEventos { get; } = new();

    private bool _hayEventos;
    public bool HayEventos
    {
        get => _hayEventos;
        set => SetProperty(ref _hayEventos, value);
    }

    public ICommand RefrescarCommand { get; }
    public ICommand CerrarSesionCommand { get; }

    public DashboardViewModel(IDashboardService dashboardService, ISessionService sessionService, IAuditoriaService auditoriaService)
    {
        _dashboardService = dashboardService;
        _sessionService = sessionService;
        _auditoriaService = auditoriaService;
        Title = "Reportes";

        var usuario = _sessionService.UsuarioActual;
        if (usuario is not null)
        {
            NombreUsuario = usuario.NombreCompleto;
            RolUsuarioTexto = TraducirRol(usuario.Rol);
            PuedeVerFinanzas = usuario.Rol is RolUsuario.Gerencia or RolUsuario.Desarrollador;
        }

        RefrescarCommand = new Command(async () => await CargarAsync());
        CerrarSesionCommand = new Command(async () => await CerrarSesionAsync());
    }

    /// <summary>Se llama desde OnAppearing para que los KPIs siempre reflejen el estado más reciente.</summary>
    public async Task CargarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var resumen = await _dashboardService.ObtenerResumenAsync();

            if (PuedeVerFinanzas)
            {
                IngresosHotelTexto = $"S/ {resumen.IngresosHotelHoy:0.00}";
                IngresosSaunaTexto = $"S/ {resumen.IngresosSaunaHoy:0.00}";
                IngresosTotalTexto = $"S/ {resumen.IngresosTotalHoy:0.00}";
                TarifaPromedioTexto = $"S/ {resumen.TarifaPromedioDiaria:0.00}";
                RevParTexto = $"S/ {resumen.IngresoPorHabitacionDisponible:0.00}";
            }
            else
            {
                IngresosHotelTexto = "***";
                IngresosSaunaTexto = "***";
                IngresosTotalTexto = "***";
                TarifaPromedioTexto = "***";
                RevParTexto = "***";
            }

            TasaOcupacionTexto = $"{resumen.TasaOcupacion:0.#}%";
            ClientesSaunaHoy = resumen.ClientesSaunaHoy;
            AlertasActivas = resumen.AlertasActivas;
            HayAlertas = resumen.AlertasActivas > 0;

            ResumenEstados.Clear();
            foreach (var item in resumen.ResumenEstados)
            {
                ResumenEstados.Add(item);
            }

            UltimosEventos.Clear();
            foreach (var evento in resumen.UltimosEventos)
            {
                UltimosEventos.Add(evento);
            }
            HayEventos = UltimosEventos.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CerrarSesionAsync()
    {
        var usuario = _sessionService.UsuarioActual;
        if (usuario is not null)
        {
            await _auditoriaService.RegistrarAsync("LOGOUT", $"{usuario.NombreCompleto} cerró sesión.", usuario.Id, "Usuario", usuario.Id);
        }

        _sessionService.CerrarSesion();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }

    private static string TraducirRol(RolUsuario rol) => rol switch
    {
        RolUsuario.Gerencia => "Gerencia",
        RolUsuario.Recepcionista => "Recepcionista",
        RolUsuario.Desarrollador => "Desarrollador",
        _ => rol.ToString()
    };
}

