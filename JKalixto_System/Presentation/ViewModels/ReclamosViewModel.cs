using System;
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

/// <summary>
/// Libro de Reclamaciones (Ley N° 29571, D.S. N° 011-2011-PCM / 042-2011-PCM):
/// todo negocio en Perú debe tenerlo. El personal lo llena a pedido del
/// consumidor; la app no necesita ser un kiosco de autoatención para cumplir con
/// la norma (esta la obliga a PONERLO A DISPOSICIÓN, no a que el consumidor lo
/// opere él mismo).
/// </summary>
public class ReclamosViewModel : BaseViewModel
{
    private readonly IReclamosService _reclamosService;
    private readonly ISessionService _sessionService;

    private List<ReclamoCardDto> _todosLosReclamos = new();

    public ObservableCollection<ReclamoCardDto> Reclamos { get; } = new();

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

    private bool _hayReclamos;
    public bool HayReclamos
    {
        get => _hayReclamos;
        set => SetProperty(ref _hayReclamos, value);
    }

    public ICommand NuevoReclamoCommand { get; }
    public ICommand SeleccionarReclamoCommand { get; }

    public ReclamosViewModel(IReclamosService reclamosService, ISessionService sessionService)
    {
        _reclamosService = reclamosService;
        _sessionService = sessionService;
        Title = "Libro de Reclamaciones";

        NuevoReclamoCommand = new Command(async () => await Shell.Current.GoToAsync(nameof(ReclamoNuevoPage)));

        SeleccionarReclamoCommand = new Command<ReclamoCardDto>(async (reclamo) =>
        {
            if (reclamo is not null)
            {
                await GestionarReclamoAsync(reclamo);
            }
        });
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
            _todosLosReclamos = await _reclamosService.ObtenerTodosAsync();
            AplicarFiltroBusqueda();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Filtra por nombre, documento o bien contratado — todo en memoria, sin
    /// volver a golpear la base de datos.</summary>
    private void AplicarFiltroBusqueda()
    {
        IEnumerable<ReclamoCardDto> query = _todosLosReclamos;

        if (!string.IsNullOrWhiteSpace(TextoBusqueda))
        {
            var texto = TextoBusqueda.Trim();
            query = query.Where(r =>
                r.NombreCompleto.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.NumeroDocumento.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                r.BienContratado.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        Reclamos.Clear();
        foreach (var r in query)
        {
            Reclamos.Add(r);
        }
        HayReclamos = Reclamos.Count > 0;
    }

    private async Task GestionarReclamoAsync(ReclamoCardDto reclamo)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is null)
        {
            return;
        }

        if (reclamo.Estado == EstadoReclamo.Respondido)
        {
            await page.DisplayAlertAsync(
                $"{reclamo.EtiquetaTipo} — {reclamo.NombreCompleto}",
                $"{reclamo.DetalleReclamo}\n\nRespondido el {reclamo.FechaRespuesta:dd/MM/yyyy}:\n{reclamo.RespuestaEstablecimiento}",
                "Cerrar");
            return;
        }

        var accion = await page.DisplayActionSheetAsync(
            $"{reclamo.EtiquetaTipo} — {reclamo.NombreCompleto}",
            "Cancelar", null,
            "Ver detalle", "Responder");

        if (accion == "Ver detalle")
        {
            var alertaPlazo = reclamo.PlazoVencido ? " — ¡PLAZO VENCIDO!" : "";
            await page.DisplayAlertAsync(
                $"{reclamo.EtiquetaTipo} — {reclamo.NombreCompleto}",
                $"Documento: {reclamo.NumeroDocumento}\n" +
                $"Bien contratado: {reclamo.BienContratado}\n" +
                $"Monto reclamado: {(reclamo.MontoReclamado.HasValue ? $"S/ {reclamo.MontoReclamado:0.00}" : "—")}\n\n" +
                $"Detalle: {reclamo.DetalleReclamo}\n\n" +
                $"Pedido del consumidor: {reclamo.PedidoConsumidor ?? "—"}\n\n" +
                $"Plazo legal de respuesta: {reclamo.FechaLimiteRespuesta:dd/MM/yyyy}{alertaPlazo}",
                "Cerrar");
        }
        else if (accion == "Responder")
        {
            var respuesta = await page.DisplayPromptAsync(
                "Responder",
                "Escribe la respuesta del establecimiento (queda registrada con la fecha de hoy):",
                "Guardar", "Cancelar",
                placeholder: "Ej: Se revisó el caso y se le ofreció...");

            if (string.IsNullOrWhiteSpace(respuesta))
            {
                return;
            }

            try
            {
                var usuarioId = _sessionService.UsuarioActual?.Id ?? 0;
                await _reclamosService.ResponderAsync(reclamo.Id, respuesta, usuarioId);
                await CargarAsync();
            }
            catch (System.InvalidOperationException ex)
            {
                await page.DisplayAlertAsync("No se pudo responder", ex.Message, "Entendido");
            }
        }
    }
}
