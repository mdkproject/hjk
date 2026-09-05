using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;
using JKalixto_System.Presentation.Pages;

namespace JKalixto_System.Presentation.ViewModels;

public class AlmacenViewModel : BaseViewModel
{
    private readonly IInventarioService _inventarioService;

    /// <summary>Copia completa traída una sola vez de la BD. El filtro de categoría se
    /// aplica en memoria — mismo patrón que RecepcionViewModel.</summary>
    private List<InsumoCardDto> _todosLosInsumos = new();

    public ObservableCollection<InsumoCardDto> Insumos { get; } = new();

    private string _filtroCategoria = "Todos";
    public string FiltroCategoria
    {
        get => _filtroCategoria;
        set
        {
            if (SetProperty(ref _filtroCategoria, value))
            {
                AplicarFiltro();
            }
        }
    }

    private string _textoBusqueda = string.Empty;
    public string TextoBusqueda
    {
        get => _textoBusqueda;
        set
        {
            if (SetProperty(ref _textoBusqueda, value))
            {
                AplicarFiltro();
            }
        }
    }

    private bool _hayInsumos;
    public bool HayInsumos
    {
        get => _hayInsumos;
        set => SetProperty(ref _hayInsumos, value);
    }

    public ICommand SeleccionarFiltroCommand { get; }
    public ICommand NuevoMovimientoCommand { get; }

    public AlmacenViewModel(IInventarioService inventarioService)
    {
        _inventarioService = inventarioService;
        Title = "Almacén";

        SeleccionarFiltroCommand = new Command<string>((categoria) =>
        {
            if (categoria is not null)
            {
                FiltroCategoria = categoria;
            }
        });

        NuevoMovimientoCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(AlmacenMovimientoPage)));
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
            _todosLosInsumos = await _inventarioService.ObtenerInsumosAsync();
            AplicarFiltro();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AplicarFiltro()
    {
        var categoria = FiltroCategoria switch
        {
            "Hotel — Habitaciones" => CategoriaInsumo.HotelHabitaciones,
            "Hotel — Cocina" => CategoriaInsumo.HotelCocina,
            "Sauna" => CategoriaInsumo.Sauna,
            _ => (CategoriaInsumo?)null
        };

        IEnumerable<InsumoCardDto> query = categoria.HasValue
            ? _todosLosInsumos.Where(i => i.Categoria == categoria.Value)
            : _todosLosInsumos;

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(i => i.Nombre.Contains(texto, System.StringComparison.OrdinalIgnoreCase));
        }

        Insumos.Clear();
        foreach (var i in query)
        {
            Insumos.Add(i);
        }
        HayInsumos = Insumos.Count > 0;
    }
}
