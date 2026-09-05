using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>Un renglón de la lista de sugerencias de autocompletado — envuelve al
/// huésped junto con si es el renglón resaltado (por mouse o por flechas del
/// teclado), para poder pintarlo distinto en XAML sin usar un IValueConverter.</summary>
public class SugerenciaHuespedItem
{
    public HabitacionCardDto Huesped { get; set; } = null!;
    public bool EsResaltado { get; set; }
    public string NombreHuesped => Huesped.NombreHuesped ?? string.Empty;
    public int Numero => Huesped.Numero;
}

public class SaunaRegistroViewModel : BaseViewModel
{
    private readonly ISaunaService _saunaService;
    private readonly ISessionService _sessionService;

    private string _tipoDocumentoTexto = "DNI";
    public string TipoDocumentoTexto
    {
        get => _tipoDocumentoTexto;
        set => SetProperty(ref _tipoDocumentoTexto, value);
    }

    private string _numeroDocumento = string.Empty;
    public string NumeroDocumento
    {
        get => _numeroDocumento;
        set => SetProperty(ref _numeroDocumento, value);
    }

    private string _nombreCompleto = string.Empty;
    public string NombreCompleto
    {
        get => _nombreCompleto;
        set => SetProperty(ref _nombreCompleto, value);
    }

    private string _numeroCandado = string.Empty;
    public string NumeroCandado
    {
        get => _numeroCandado;
        set => SetProperty(ref _numeroCandado, value);
    }

    private bool _esSeccionDamas = true;
    public bool EsSeccionDamas
    {
        get => _esSeccionDamas;
        set => SetProperty(ref _esSeccionDamas, value);
    }

    /// <summary>Copia completa de huéspedes activos del hotel, traída una vez en
    /// CargarAsync — el autocompletado filtra esto en memoria mientras el usuario
    /// escribe, sin volver a golpear la base de datos por cada letra.</summary>
    public ObservableCollection<HabitacionCardDto> HuespedesActivos { get; } = new();

    private List<HabitacionCardDto> _coincidenciasActuales = new();

    /// <summary>Lista visible del desplegable de autocompletado (máx. 8), ya envuelta
    /// en SugerenciaHuespedItem para poder marcar cuál está resaltada.</summary>
    public ObservableCollection<SugerenciaHuespedItem> Sugerencias { get; } = new();

    private bool _mostrarSugerencias;
    public bool MostrarSugerencias
    {
        get => _mostrarSugerencias;
        set => SetProperty(ref _mostrarSugerencias, value);
    }

    private int _indiceResaltado = -1;
    public int IndiceResaltado
    {
        get => _indiceResaltado;
        set => SetProperty(ref _indiceResaltado, value);
    }

    private string _busquedaHuesped = string.Empty;

    /// <summary>Reemplaza al viejo toggle "Es huésped del Hotel" + Picker: el usuario
    /// simplemente escribe el nombre acá y elige de la lista de sugerencias (con
    /// mouse o flechas ↑/↓ + Enter). Si el texto no coincide con nadie, no se vincula
    /// a ninguna habitación y el cliente paga normal — no hace falta un switch aparte
    /// para eso, ya que HuespedSeleccionado nulo ya significa "no es huésped".</summary>
    public string BusquedaHuesped
    {
        get => _busquedaHuesped;
        set
        {
            if (SetProperty(ref _busquedaHuesped, value))
            {
                if (HuespedSeleccionado is not null &&
                    !string.Equals(value, HuespedSeleccionado.NombreHuesped, System.StringComparison.Ordinal))
                {
                    // El usuario siguió editando después de haber elegido uno — se
                    // desvincula, como corresponde (ya no coincide con lo elegido).
                    HuespedSeleccionado = null;
                }
                ActualizarSugerencias();
            }
        }
    }

    private HabitacionCardDto? _huespedSeleccionado;
    public HabitacionCardDto? HuespedSeleccionado
    {
        get => _huespedSeleccionado;
        private set
        {
            if (SetProperty(ref _huespedSeleccionado, value))
            {
                OnPropertyChanged(nameof(EsHuespedHotel));
            }
        }
    }

    /// <summary>Ahora es un valor calculado, no un toggle manual: es huésped del hotel
    /// si (y solo si) el texto escrito coincidió con alguien y fue seleccionado. Evita
    /// el estado inconsistente de antes (switch en On sin nadie elegido en el Picker).</summary>
    public bool EsHuespedHotel => HuespedSeleccionado is not null;

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

    public ICommand SeleccionarTipoDocumentoCommand { get; }
    public ICommand SeleccionarSugerenciaCommand { get; }
    public ICommand MoverResaltadoCommand { get; }
    public ICommand ConfirmarResaltadoCommand { get; }
    public ICommand QuitarHuespedCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public SaunaRegistroViewModel(ISaunaService saunaService, ISessionService sessionService)
    {
        _saunaService = saunaService;
        _sessionService = sessionService;
        Title = "Nuevo Cliente — Sauna";

        SeleccionarTipoDocumentoCommand = new Command<string>((tipo) =>
        {
            if (tipo is not null)
            {
                TipoDocumentoTexto = tipo;
            }
        });

        SeleccionarSugerenciaCommand = new Command<SugerenciaHuespedItem>((item) =>
        {
            if (item is not null)
            {
                ConfirmarSeleccion(item.Huesped);
            }
        });

        MoverResaltadoCommand = new Command<string>((direccion) =>
        {
            if (_coincidenciasActuales.Count == 0)
            {
                return;
            }

            if (direccion == "Abajo")
            {
                IndiceResaltado = (IndiceResaltado + 1) % _coincidenciasActuales.Count;
            }
            else if (direccion == "Arriba")
            {
                IndiceResaltado = IndiceResaltado <= 0 ? _coincidenciasActuales.Count - 1 : IndiceResaltado - 1;
            }

            ReconstruirListaSugerencias();
        });

        ConfirmarResaltadoCommand = new Command(() =>
        {
            if (IndiceResaltado >= 0 && IndiceResaltado < _coincidenciasActuales.Count)
            {
                ConfirmarSeleccion(_coincidenciasActuales[IndiceResaltado]);
            }
        });

        QuitarHuespedCommand = new Command(() =>
        {
            HuespedSeleccionado = null;
            BusquedaHuesped = string.Empty;
        });

        ConfirmarCommand = new Command(async () => await ConfirmarAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public async Task CargarAsync()
    {
        var huespedes = await _saunaService.BuscarHuespedesActivosAsync();
        HuespedesActivos.Clear();
        foreach (var h in huespedes)
        {
            HuespedesActivos.Add(h);
        }
    }

    /// <summary>Filtra HuespedesActivos en memoria por el texto escrito (máx. 8
    /// resultados) cada vez que BusquedaHuesped cambia.</summary>
    private void ActualizarSugerencias()
    {
        var texto = BusquedaHuesped.Trim();

        _coincidenciasActuales = HuespedSeleccionado is not null || texto.Length == 0
            ? new List<HabitacionCardDto>()
            : HuespedesActivos
                .Where(h => (h.NombreHuesped ?? string.Empty).Contains(texto, System.StringComparison.OrdinalIgnoreCase))
                .Take(8)
                .ToList();

        MostrarSugerencias = _coincidenciasActuales.Count > 0;
        IndiceResaltado = _coincidenciasActuales.Count > 0 ? 0 : -1;
        ReconstruirListaSugerencias();
    }

    private void ReconstruirListaSugerencias()
    {
        Sugerencias.Clear();
        for (var i = 0; i < _coincidenciasActuales.Count; i++)
        {
            Sugerencias.Add(new SugerenciaHuespedItem
            {
                Huesped = _coincidenciasActuales[i],
                EsResaltado = i == IndiceResaltado
            });
        }
    }

    private void ConfirmarSeleccion(HabitacionCardDto huesped)
    {
        HuespedSeleccionado = huesped;
        _busquedaHuesped = huesped.NombreHuesped ?? string.Empty;
        OnPropertyChanged(nameof(BusquedaHuesped));

        _coincidenciasActuales = new List<HabitacionCardDto>();
        MostrarSugerencias = false;
        IndiceResaltado = -1;
        ReconstruirListaSugerencias();
    }

    private async Task ConfirmarAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(NumeroDocumento) || string.IsNullOrWhiteSpace(NombreCompleto) || string.IsNullOrWhiteSpace(NumeroCandado))
        {
            MensajeError = "El número de documento, nombre y número de candado son obligatorios.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
            var tipoDocumento = TipoDocumentoTexto switch
            {
                "Pasaporte" => TipoDocumento.Pasaporte,
                "Carné Ext." => TipoDocumento.CarneExtranjeria,
                _ => TipoDocumento.DNI
            };

            await _saunaService.RegistrarClienteAsync(new NuevoClienteSaunaDto
            {
                TipoDocumento = tipoDocumento,
                NumeroDocumento = NumeroDocumento.Trim(),
                NombreCompleto = NombreCompleto.Trim(),
                NumeroCandado = NumeroCandado.Trim(),
                Seccion = EsSeccionDamas ? SeccionSauna.Damas : SeccionSauna.General,
                EsHuespedHotel = EsHuespedHotel,
                EstadiaHotelId = HuespedSeleccionado?.EstadiaId
            }, usuarioId);

            var horaRegistro = DateTime.Now;
            var page = Shell.Current?.CurrentPage;
            if (page is not null)
            {
                await page.DisplayAlertAsync(
                    "Cliente de Sauna registrado",
                    $"{NombreCompleto.Trim()} — Candado {NumeroCandado.Trim()}\nFecha y hora de registro: {horaRegistro:dd/MM/yyyy HH:mm:ss}",
                    "OK");
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (System.InvalidOperationException ex)
        {
            MensajeError = ex.Message;
            HayError = true;
        }
        catch (System.Exception)
        {
            MensajeError = "No se pudo registrar el cliente. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
