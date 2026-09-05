using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using JKalixto_System.Domain.Models;
using JKalixto_System.Application.Services;

namespace JKalixto_System.Presentation.ViewModels;

public class AuditoriaViewModel : BaseViewModel
{
    private readonly IAuditoriaService _auditoriaService;

    private List<LogAuditoria> _todosLosEventos = new();

    public ObservableCollection<LogAuditoria> Eventos { get; } = new();

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
            _todosLosEventos = await _auditoriaService.ObtenerRecientesAsync(200);
            AplicarFiltroBusqueda();
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

    /// <summary>Filtra por descripción, usuario o tipo de acción — sobre los 200 eventos
    /// más recientes que ya se trajeron de la base, sin volver a golpearla.</summary>
    private void AplicarFiltroBusqueda()
    {
        IEnumerable<LogAuditoria> query = _todosLosEventos;

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(e =>
                e.Descripcion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                e.UsuarioNombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                e.TipoAccion.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        Eventos.Clear();
        foreach (var e in query)
        {
            Eventos.Add(e);
        }
        HayEventos = Eventos.Count > 0;
    }
}
