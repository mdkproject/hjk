using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class CalendarioViewModel : BaseViewModel
{
    private readonly ICalendarioService _calendarioService;

    private int _anio;
    private int _mes;

    /// <summary>Copia completa (36 habitaciones) traída del servicio para el mes visible.
    /// Los filtros de piso/tipo se aplican en memoria sobre esta copia — así cambiar de
    /// filtro es instantáneo y no vuelve a golpear la base de datos.</summary>
    private CalendarioMensualDto? _calendarioCompleto;

    /// <summary>Versión ya filtrada por piso/tipo — es la que dibuja la página.</summary>
    public CalendarioMensualDto? Calendario { get; private set; }

    private string _mesTexto = string.Empty;
    public string MesTexto
    {
        get => _mesTexto;
        set => SetProperty(ref _mesTexto, value);
    }

    private bool _esMesActual;
    public bool EsMesActual
    {
        get => _esMesActual;
        set
        {
            if (SetProperty(ref _esMesActual, value))
            {
                OnPropertyChanged(nameof(NoEsMesActual));
            }
        }
    }

    public bool NoEsMesActual => !EsMesActual;

    private string _pisoFiltro = "Todos";
    public string PisoFiltro
    {
        get => _pisoFiltro;
        set
        {
            if (SetProperty(ref _pisoFiltro, value))
            {
                AplicarFiltros();
            }
        }
    }

    private string _tipoFiltro = "Todos";
    public string TipoFiltro
    {
        get => _tipoFiltro;
        set
        {
            if (SetProperty(ref _tipoFiltro, value))
            {
                AplicarFiltros();
            }
        }
    }

    /// <summary>Filtra las FILAS (habitaciones) por su estado ACTUAL (el mismo dato de
    /// Registro Hotel) — no por el color de cada día del mes. Además de filtrar,
    /// también reduce cuántas filas se dibujan a la vez, lo que ayuda al rendimiento
    /// cuando el usuario quiere ver, por ejemplo, solo las habitaciones ocupadas hoy.</summary>
    private string _estadoFiltro = "Todos";
    public string EstadoFiltro
    {
        get => _estadoFiltro;
        set
        {
            if (SetProperty(ref _estadoFiltro, value))
            {
                AplicarFiltros();
            }
        }
    }

    private string _textoBusqueda = string.Empty;
    /// <summary>Busca por número de habitación o por el nombre de cualquier huésped
    /// que aparezca en ese mes (aunque no esté activo hoy) — coincide con la lógica de
    /// "encontrar dónde estuvo/está tal persona este mes".</summary>
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set
        {
            if (SetProperty(ref _textoBusqueda, value))
            {
                AplicarFiltros();
            }
        }
    }

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

    /// <summary>Se dispara cada vez que hay una grilla nueva para dibujar — la página
    /// escucha esto para reconstruir el Grid dinámico (no se puede hacer con bindings
    /// normales porque la cantidad de filas/columnas cambia según el mes y los filtros).</summary>
    public event Action? CalendarioActualizado;

    public ICommand MesAnteriorCommand { get; }
    public ICommand MesSiguienteCommand { get; }
    public ICommand IrAHoyCommand { get; }
    public ICommand SeleccionarPisoFiltroCommand { get; }
    public ICommand SeleccionarTipoFiltroCommand { get; }
    public ICommand SeleccionarEstadoFiltroCommand { get; }

    public CalendarioViewModel(ICalendarioService calendarioService)
    {
        _calendarioService = calendarioService;
        Title = "Calendario";

        var hoy = DateTime.Now;
        _anio = hoy.Year;
        _mes = hoy.Month;

        MesAnteriorCommand = new Command(async () => await CambiarMesAsync(-1));
        MesSiguienteCommand = new Command(async () => await CambiarMesAsync(1));
        IrAHoyCommand = new Command(async () => await IrAHoyAsync());

        SeleccionarPisoFiltroCommand = new Command<string>((piso) =>
        {
            if (piso is not null)
            {
                PisoFiltro = piso;
            }
        });

        SeleccionarTipoFiltroCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                TipoFiltro = tipo;
            }
        });

        SeleccionarEstadoFiltroCommand = new Command<string>((estado) =>
        {
            if (estado is not null)
            {
                EstadoFiltro = estado;
            }
        });
    }

    public async Task CargarAsync()
    {
        // Si ya había un calendario cargado (por ejemplo, volviendo de otra pantalla),
        // no reinicia el mes que el usuario tenía elegido — solo refresca los datos.
        if (_calendarioCompleto is null)
        {
            var hoy = DateTime.Now;
            _anio = hoy.Year;
            _mes = hoy.Month;
        }

        await CargarCalendarioAsync();
    }

    /// <summary>Cambia de mes manteniendo el día pedido desde el mini-calendario (si
    /// aplica) o simplemente avanza/retrocede un mes desde la navegación normal.</summary>
    private async Task CambiarMesAsync(int delta)
    {
        var fecha = new DateTime(_anio, _mes, 1).AddMonths(delta);
        _anio = fecha.Year;
        _mes = fecha.Month;
        await CargarCalendarioAsync();
    }

    private async Task IrAHoyAsync()
    {
        var hoy = DateTime.Now;
        _anio = hoy.Year;
        _mes = hoy.Month;
        await CargarCalendarioAsync();
    }

    private async Task CargarCalendarioAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            _calendarioCompleto = await _calendarioService.ObtenerCalendarioMensualAsync(_anio, _mes);
            MesTexto = _calendarioCompleto.NombreMesTexto;

            var hoy = DateTime.Now;
            EsMesActual = _anio == hoy.Year && _mes == hoy.Month;

            AplicarFiltros();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Filtra en memoria las habitaciones (columnas) del calendario completo por
    /// piso y tipo, y reconstruye la versión filtrada que dibuja la página. Mismo patrón
    /// que RecepcionViewModel.AplicarFiltros — evita volver a golpear la base de datos
    /// cada vez que el usuario cambia de filtro.</summary>
    private void AplicarFiltros()
    {
        if (_calendarioCompleto is null)
        {
            return;
        }

        IEnumerable<ColumnaHabitacionCalendarioDto> query = _calendarioCompleto.Columnas;

        if (PisoFiltro != "Todos" && int.TryParse(PisoFiltro, out var piso))
        {
            query = query.Where(c => c.Piso == piso);
        }

        if (TipoFiltro != "Todos")
        {
            var tipo = TipoFiltro switch
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
                query = query.Where(c => c.Tipo == tipo.Value);
            }
        }

        // Los contadores se calculan sobre lo filtrado por piso+tipo, ANTES del filtro
        // de estado — así siguen siendo útiles como referencia general aunque el
        // usuario ya esté mirando un solo estado (mismo patrón que RecepcionViewModel).
        var paraContar = query.ToList();
        ContadorDisponible = paraContar.Count(c => c.EstadoActual == EstadoHabitacion.Disponible);
        ContadorOcupada = paraContar.Count(c => c.EstadoActual == EstadoHabitacion.Ocupada);
        ContadorLimpieza = paraContar.Count(c => c.EstadoActual == EstadoHabitacion.LimpiezaSalida);
        ContadorMantenimiento = paraContar.Count(c => c.EstadoActual == EstadoHabitacion.Mantenimiento);

        query = paraContar;
        if (EstadoFiltro != "Todos")
        {
            var estado = EstadoFiltro switch
            {
                "Disponible" => EstadoHabitacion.Disponible,
                "Ocupada" => EstadoHabitacion.Ocupada,
                "Limpieza" => EstadoHabitacion.LimpiezaSalida,
                "Mantenimiento" => EstadoHabitacion.Mantenimiento,
                _ => (EstadoHabitacion?)null
            };
            if (estado.HasValue)
            {
                query = query.Where(c => c.EstadoActual == estado.Value);
            }
        }

        // El buscador se aplica al final: por número de habitación, o por el nombre de
        // CUALQUIER huésped que haya pasado por esa habitación en el mes visible (no
        // solo el de hoy) — así sirve para encontrar dónde estuvo alguien ese mes.
        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(c =>
                c.Numero.ToString().Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                c.Celdas.Any(celda => celda.NombreCliente?.Contains(texto, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Calendario = new CalendarioMensualDto
        {
            Anio = _calendarioCompleto.Anio,
            Mes = _calendarioCompleto.Mes,
            NombreMesTexto = _calendarioCompleto.NombreMesTexto,
            Dias = _calendarioCompleto.Dias,
            Columnas = query.ToList()
        };

        CalendarioActualizado?.Invoke();
    }
}
