using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class SaunaVentaViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ISaunaService _saunaService;
    private readonly ISessionService _sessionService;

    public int ClienteSaunaId { get; private set; }

    /// <summary>Solo se llena cuando la venta viene de Cafetería como cargo DIRECTO a
    /// una habitación (sin cliente de Sauna de por medio) — ver ApplyQueryAttributes
    /// y ConfirmarAsync, que elige qué método del servicio llamar según esto.</summary>
    public int? EstadiaIdDirecta { get; private set; }

    private string _nombreCliente = string.Empty;
    public string NombreCliente
    {
        get => _nombreCliente;
        set => SetProperty(ref _nombreCliente, value);
    }

    private bool _esHuespedHotel;
    public bool EsHuespedHotel
    {
        get => _esHuespedHotel;
        set => SetProperty(ref _esHuespedHotel, value);
    }

    public ObservableCollection<ProductoCatalogoDto> Catalogo { get; } = new();
    public ObservableCollection<ItemCarritoDto> Carrito { get; } = new();

    private string _totalTexto = "S/ 0.00";
    public string TotalTexto
    {
        get => _totalTexto;
        set => SetProperty(ref _totalTexto, value);
    }

    private bool _hayItems;
    public bool HayItems
    {
        get => _hayItems;
        set => SetProperty(ref _hayItems, value);
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

    public ICommand AgregarProductoCommand { get; }
    public ICommand QuitarItemCommand { get; }
    public ICommand ConfirmarVentaCommand { get; }
    public ICommand CancelarCommand { get; }

    public SaunaVentaViewModel(ISaunaService saunaService, ISessionService sessionService)
    {
        _saunaService = saunaService;
        _sessionService = sessionService;
        Title = "Venta POS";

        AgregarProductoCommand = new Command<ProductoCatalogoDto>(async (producto) =>
        {
            if (producto is null)
            {
                return;
            }

            if (producto.EsAlquilerVenta)
            {
                var page = Shell.Current?.CurrentPage;
                if (page is null)
                {
                    return;
                }

                var opcion = await page.DisplayActionSheetAsync(
                    producto.Nombre,
                    "Cancelar", null,
                    $"Alquiler — S/ {producto.PrecioAlquiler:0.00}",
                    $"Venta — S/ {producto.PrecioVenta:0.00}");

                if (opcion is null || opcion == "Cancelar")
                {
                    return;
                }

                var esAlquiler = opcion.StartsWith("Alquiler");
                var descripcion = esAlquiler ? $"{producto.Nombre} (Alquiler)" : $"{producto.Nombre} (Venta)";
                var precio = esAlquiler ? producto.PrecioAlquiler : producto.PrecioVenta;

                AgregarAlCarrito(producto.ProductoId, descripcion, precio);
                return;
            }

            AgregarAlCarrito(producto.ProductoId, producto.Nombre, producto.Precio);
        });

        QuitarItemCommand = new Command<ItemCarritoDto>((item) =>
        {
            if (item is not null)
            {
                Carrito.Remove(item);
                RecalcularTotal();
            }
        });

        ConfirmarVentaCommand = new Command(async () => await ConfirmarVentaAsync());
        CancelarCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("clienteSaunaId", out var idValue) && int.TryParse(idValue?.ToString(), out var id))
        {
            ClienteSaunaId = id;
        }

        // Llega desde Cafetería cuando se cobra directo a una habitación, sin pasar
        // por un registro de ClienteSauna (ver CafeteriaViewModel).
        if (query.TryGetValue("estadiaId", out var estadiaValue) && int.TryParse(estadiaValue?.ToString(), out var estadiaId))
        {
            EstadiaIdDirecta = estadiaId;
            EsHuespedHotel = true;
        }

        if (query.TryGetValue("esHuespedHotel", out var huespedValue) && huespedValue?.ToString() == "1")
        {
            EsHuespedHotel = true;
        }

        if (query.TryGetValue("nombre", out var nombreValue) && nombreValue is not null)
        {
            NombreCliente = nombreValue.ToString() ?? string.Empty;
        }

        Title = query.TryGetValue("numero", out var numeroValue) && numeroValue is not null
            ? $"Cafetería — Hab. {numeroValue}"
            : $"Venta POS — {NombreCliente}";
    }

    public async Task CargarAsync()
    {
        if (Catalogo.Count > 0)
        {
            return; // el catálogo no cambia durante la venta, no hace falta recargarlo cada vez
        }

        var productos = await _saunaService.ObtenerCatalogoAsync();
        Catalogo.Clear();
        foreach (var p in productos)
        {
            Catalogo.Add(p);
        }
    }

    private void AgregarAlCarrito(int productoId, string descripcion, decimal precioUnitario)
    {
        // Dos ítems se consideran "el mismo" solo si coinciden producto Y descripción
        // (así "Toalla (Alquiler)" y "Toalla (Venta)" quedan como líneas separadas
        // en el carrito, aunque sean el mismo ProductoId).
        var existente = Carrito.FirstOrDefault(i => i.ProductoId == productoId && i.Descripcion == descripcion);
        if (existente is not null)
        {
            Carrito.Remove(existente);
            Carrito.Add(new ItemCarritoDto
            {
                ProductoId = existente.ProductoId,
                Descripcion = existente.Descripcion,
                PrecioUnitario = existente.PrecioUnitario,
                Cantidad = existente.Cantidad + 1
            });
        }
        else
        {
            Carrito.Add(new ItemCarritoDto
            {
                ProductoId = productoId,
                Descripcion = descripcion,
                PrecioUnitario = precioUnitario,
                Cantidad = 1
            });
        }

        RecalcularTotal();
    }

    private void RecalcularTotal()
    {
        var total = Carrito.Sum(i => i.Subtotal);
        TotalTexto = $"S/ {total:0.00}";
        HayItems = Carrito.Count > 0;
    }

    private async Task ConfirmarVentaAsync()
    {
        if (IsBusy)
        {
            return;
        }

        HayError = false;
        MensajeError = string.Empty;

        if (Carrito.Count == 0)
        {
            MensajeError = "Agrega al menos un producto antes de confirmar.";
            HayError = true;
            return;
        }

        try
        {
            IsBusy = true;
            var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
            var cargarAHabitacion = false;

            if (EsHuespedHotel)
            {
                var page = Shell.Current?.CurrentPage;
                if (page is not null)
                {
                    var accion = await page.DisplayActionSheetAsync(
                        $"Total: {TotalTexto}",
                        "Cancelar", null,
                        "Cargar a la habitación", "Cobrar ahora");

                    if (accion == "Cancelar" || accion is null)
                    {
                        return;
                    }

                    cargarAHabitacion = accion == "Cargar a la habitación";
                }
            }

            if (EstadiaIdDirecta.HasValue)
            {
                await _saunaService.RegistrarVentaHotelAsync(EstadiaIdDirecta.Value, Carrito.ToList(), usuarioId, cargarAHabitacion);
            }
            else
            {
                await _saunaService.RegistrarVentaAsync(ClienteSaunaId, Carrito.ToList(), usuarioId, cargarAHabitacion);
            }

            var horaRegistro = System.DateTime.Now;
            var pageActual = Shell.Current?.CurrentPage;
            if (pageActual is not null)
            {
                await pageActual.DisplayAlertAsync(
                    "Venta registrada",
                    $"{NombreCliente} — Total {TotalTexto}\nFecha y hora de registro: {horaRegistro:dd/MM/yyyy HH:mm:ss}",
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
            MensajeError = "No se pudo registrar la venta. Intente nuevamente.";
            HayError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
