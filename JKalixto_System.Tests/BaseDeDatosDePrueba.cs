using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Infrastructure.Data;

namespace JKalixto_System.Tests;

/// <summary>
/// Crea una base de datos SQLite REAL (un archivo temporal, no InMemory) para cada
/// prueba, con el mismo esquema y los mismos datos semilla (usuarios, 36
/// habitaciones, catálogo POS, insumos) que usa la app de verdad. Se prueba contra
/// SQLite real —no un proveedor "InMemory" de EF Core— porque el objetivo es
/// detectar problemas que solo aparecen con el motor real (constraints, tokens de
/// concurrencia, etc.), no solo errores de lógica en C#.
/// </summary>
public sealed class BaseDeDatosDePrueba : IDisposable
{
    private readonly string _rutaArchivo;

    public AppDbContext Contexto { get; }

    public BaseDeDatosDePrueba()
    {
        _rutaArchivo = Path.Combine(Path.GetTempPath(), $"jkalixto_test_{Guid.NewGuid():N}.db");
        Contexto = NuevoContexto();
        Contexto.Database.EnsureCreated();
    }

    /// <summary>
    /// Abre OTRA conexión a la MISMA base física, con su propio AppDbContext — tal
    /// como pasa en la app real (AppDbContext está registrado "Transient": cada
    /// pantalla/servicio recibe su propia instancia). Se usa para reproducir
    /// condiciones de carrera reales entre "dos usuarios/terminales" en vez de
    /// simular todo sobre un único contexto.
    /// </summary>
    public AppDbContext NuevoContexto()
    {
        // Misma configuración que MauiProgram.cs: mismo "Default Timeout", y
        // NoTracking por defecto — así, si algún método real se olvida de pedir
        // ".AsTracking()" donde hace falta (lee una fila, la modifica y la guarda),
        // las pruebas lo detectan igual que pasaría en la app real, en vez de
        // "funcionar en la prueba" solo porque EF Core rastrea todo por defecto.
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_rutaArchivo};Default Timeout=5")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new AppDbContext(opciones);
    }

    public void Dispose()
    {
        Contexto.Dispose();
        try
        {
            if (File.Exists(_rutaArchivo))
            {
                File.Delete(_rutaArchivo);
            }
        }
        catch
        {
            // Best effort: si Windows todavía tiene el archivo agarrado, no hace
            // falta tumbar la prueba por eso.
        }
    }
}
