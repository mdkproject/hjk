namespace JKalixto_System.Web.Services;

/// <summary>
/// Es el mecanismo que hace posible la prueba concreta que motivó esta migración:
/// que la laptop vea, EN VIVO, un cambio que acaba de hacer la PC (y viceversa),
/// sin recargar la página a mano.
///
/// Blazor Server NO hace esto solo: cada pestaña/máquina conectada vive en su
/// propio "circuito" con su propio scope de DI (por eso AppDbContext, servicios,
/// etc. son Scoped en Program.cs) — un circuito no se entera solo de lo que pasó
/// en otro. Este servicio SÍ es Singleton a propósito: es el único punto
/// compartido entre TODOS los circuitos conectados al mismo servidor.
///
/// Flujo: un componente Razor (ej. Recepcion.razor) se suscribe al evento en
/// OnInitialized y llama StateHasChanged() en el handler; cuando cualquier
/// usuario (de cualquier máquina) hace un cambio que afecta el tablero, avisa acá
/// con Avisar("habitaciones"). Todos los componentes suscritos a esa misma clave
/// se refrescan solos. Debe des-suscribirse en Dispose para no acumular handlers
/// "fantasma" de pestañas ya cerradas.
/// </summary>
public interface INotificadorCambios
{
    /// <summary>Se dispara cuando algo cambió. El string es una clave simple del área
    /// afectada (ej. "habitaciones") — no hace falta más detalle: cada suscriptor
    /// simplemente vuelve a pedir sus datos frescos, es más simple y más robusto
    /// que tratar de mandar el objeto exacto que cambió.</summary>
    event Action<string>? CambioDetectado;

    /// <summary>Llamado por el propio componente Razor justo después de guardar un
    /// cambio (ej. tras un check-in exitoso), para avisar a todos los demás
    /// circuitos conectados. A propósito NO se llama desde Application/Services —
    /// esa capa es compartida con MAUI y no debe depender de nada de Blazor.</summary>
    void Avisar(string area);
}

public class NotificadorCambios : INotificadorCambios
{
    public event Action<string>? CambioDetectado;

    public void Avisar(string area)
    {
        CambioDetectado?.Invoke(area);
    }
}
