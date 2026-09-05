using System.Collections.ObjectModel;
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

    public ObservableCollection<RegistroHuespedDto> Registro { get; } = new();

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
            var lista = await _registroHuespedesService.ObtenerRegistroAsync();

            Registro.Clear();
            foreach (var r in lista)
            {
                Registro.Add(r);
            }
            HayRegistros = Registro.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
