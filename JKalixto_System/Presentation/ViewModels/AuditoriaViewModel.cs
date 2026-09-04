using System.Collections.ObjectModel;
using System.Threading.Tasks;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class AuditoriaViewModel : BaseViewModel
{
    private readonly IAuditoriaService _auditoriaService;

    public ObservableCollection<LogAuditoria> Eventos { get; } = new();

    private bool _hayEventos;
    public bool HayEventos
    {
        get => _hayEventos;
        set => SetProperty(ref _hayEventos, value);
    }

    public AuditoriaViewModel(IAuditoriaService auditoriaService)
    {
        _auditoriaService = auditoriaService;
        Title = "Ver Auditoría";
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
            var eventos = await _auditoriaService.ObtenerRecientesAsync(200);

            Eventos.Clear();
            foreach (var e in eventos)
            {
                Eventos.Add(e);
            }
            HayEventos = Eventos.Count > 0;
        }
        catch (System.UnauthorizedAccessException ex)
        {
            // En teoría no debería pasar nunca (el menú ya oculta esta pantalla a
            // quien no sea Gerencia/Desarrollador), pero si de alguna forma se
            // llega igual, no debe tumbar la app — se avisa y se vuelve atrás.
            var page = Microsoft.Maui.Controls.Shell.Current?.CurrentPage;
            if (page is not null)
            {
                await page.DisplayAlertAsync("Acceso restringido", ex.Message, "Entendido");
            }
            await Microsoft.Maui.Controls.Shell.Current!.GoToAsync("..");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
