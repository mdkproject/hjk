using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

/// <summary>Registro de Huéspedes exigido por MINCETUR (D.S. N° 001-2015-MINCETUR,
/// modificado por D.S. N° 005-2021-MINCETUR) — de solo lectura: los datos se
/// capturan en el Check-in (ver CheckInViewModel), acá solo se listan en el
/// formato/columnas que pide la norma.</summary>
public class RegistroHuespedesViewModel : BaseViewModel
{
    private readonly IRegistroHuespedesService _registroHuespedesService;

    private List<RegistroHuespedDto> _todoElRegistro = new();

    public ObservableCollection<RegistroHuespedDto> Registro { get; } = new();

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

    private bool _hayRegistros;
    public bool HayRegistros
    {
        get => _hayRegistros;
        set => SetProperty(ref _hayRegistros, value);
    }

    public RegistroHuespedesViewModel(IRegistroHuespedesService registroHuespedesService)
    {
        _registroHuespedesService = registroHuespedesService;
        Title = "Registro de Huéspedes";
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
            _todoElRegistro = await _registroHuespedesService.ObtenerRegistroAsync();
            AplicarFiltroBusqueda();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Filtra por nombre o documento — todo en memoria, sin volver a golpear
    /// la base de datos.</summary>
    private void AplicarFiltroBusqueda()
    {
        IEnumerable<RegistroHuespedDto> query = _todoElRegistro;

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(r =>
                r.NombreCompleto.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        Registro.Clear();
        foreach (var r in query)
        {
            Registro.Add(r);
        }
        HayRegistros = Registro.Count > 0;
    }
}
