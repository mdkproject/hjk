using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using JKalixto_System.Domain.Models;

namespace JKalixto_System.Infrastructure.Data;

/// <summary>
/// Puerta de entrada a la base de datos SQLite. Contiene Usuarios, Habitaciones,
/// Estadias/Acompanantes (Hotel) y ya deja preparadas las tablas de Sauna/POS/
/// Auditoría/CierreCaja para los siguientes módulos.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Habitacion> Habitaciones => Set<Habitacion>();
    public DbSet<Estadia> Estadias => Set<Estadia>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<Acompanante> Acompanantes => Set<Acompanante>();
    public DbSet<AcompananteReserva> AcompanantesReserva => Set<AcompananteReserva>();
    public DbSet<ClienteSauna> ClientesSauna => Set<ClienteSauna>();
    public DbSet<ProductoPOS> ProductosPOS => Set<ProductoPOS>();
    public DbSet<VentaSauna> VentasSauna => Set<VentaSauna>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Penalidad> Penalidades => Set<Penalidad>();
    public DbSet<CierreCaja> CierresCaja => Set<CierreCaja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();
    public DbSet<LogAuditoria> LogsAuditoria => Set<LogAuditoria>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ----------------------------------------------------------------
        // USUARIOS
        // ----------------------------------------------------------------
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
            entity.Property(u => u.NombreCompleto).IsRequired().HasMaxLength(100);
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        // ----------------------------------------------------------------
        // HABITACIONES
        // ----------------------------------------------------------------
        modelBuilder.Entity<Habitacion>(entity =>
        {
            entity.ToTable("Habitaciones");
            entity.HasKey(h => h.Id);
            entity.HasIndex(h => h.Numero).IsUnique();
            entity.Property(h => h.TarifaNoche).HasPrecision(10, 2);

            // Token de concurrencia: EF Core incluye el valor de Estado que se leyó
            // en el WHERE del UPDATE. Si dos recepcionistas leen "Disponible" casi al
            // mismo tiempo y ambos intentan hacer Check-in, el primero en guardar
            // gana; el segundo UPDATE no encuentra ninguna fila que siga en el estado
            // viejo y EF Core lanza DbUpdateConcurrencyException en vez de dejar la
            // misma habitación asignada a dos huéspedes. Ver HabitacionService.CheckInAsync
            // y ReservaService.ConvertirEnCheckInAsync, que atrapan esa excepción.
            entity.Property(h => h.Estado).IsConcurrencyToken();
        });

        // ----------------------------------------------------------------
        // RESERVAS (reservas a futuro, separadas de las Estadias reales)
        // ----------------------------------------------------------------
        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("Reservas");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.NumeroDocumento).IsRequired().HasMaxLength(20);
            entity.Property(r => r.NombreCompleto).IsRequired().HasMaxLength(150);

            entity.HasOne(r => r.Habitacion)
                  .WithMany()
                  .HasForeignKey(r => r.HabitacionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.Acompanantes)
                  .WithOne()
                  .HasForeignKey(a => a.ReservaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AcompananteReserva>(entity =>
        {
            entity.ToTable("AcompanantesReserva");
            entity.HasKey(a => a.Id);
        });

        // ----------------------------------------------------------------
        // ESTADIAS + ACOMPAÑANTES
        // ----------------------------------------------------------------
        modelBuilder.Entity<Estadia>(entity =>
        {
            entity.ToTable("Estadias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroDocumento).IsRequired().HasMaxLength(20);
            entity.Property(e => e.NombreCompleto).IsRequired().HasMaxLength(150);
            entity.Property(e => e.TotalAcumulado).HasPrecision(10, 2);

            entity.HasOne(e => e.Habitacion)
                  .WithMany()
                  .HasForeignKey(e => e.HabitacionId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Acompanantes)
                  .WithOne()
                  .HasForeignKey(a => a.EstadiaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Acompanante>(entity =>
        {
            entity.ToTable("Acompanantes");
            entity.HasKey(a => a.Id);
        });

        // ----------------------------------------------------------------
        // SAUNA + POS (tablas listas; la lógica se activa en el próximo módulo)
        // ----------------------------------------------------------------
        modelBuilder.Entity<ClienteSauna>(entity =>
        {
            entity.ToTable("ClientesSauna");
            entity.HasKey(c => c.Id);
        });

        modelBuilder.Entity<ProductoPOS>(entity =>
        {
            entity.ToTable("ProductosPOS");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Precio).HasPrecision(10, 2);
            entity.Property(p => p.PrecioAlquiler).HasPrecision(10, 2);
            entity.Property(p => p.PrecioVenta).HasPrecision(10, 2);
        });

        modelBuilder.Entity<VentaSauna>(entity =>
        {
            entity.ToTable("VentasSauna");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Total).HasPrecision(10, 2);

            entity.HasMany(v => v.Detalles)
                  .WithOne()
                  .HasForeignKey(d => d.VentaSaunaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DetalleVenta>(entity =>
        {
            entity.ToTable("DetallesVenta");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.PrecioUnitario).HasPrecision(10, 2);
            entity.Property(d => d.Subtotal).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Penalidad>(entity =>
        {
            entity.ToTable("Penalidades");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Monto).HasPrecision(10, 2);
        });

        modelBuilder.Entity<CierreCaja>(entity =>
        {
            entity.ToTable("CierresCaja");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.TotalHotel).HasPrecision(10, 2);
            entity.Property(c => c.TotalSauna).HasPrecision(10, 2);
        });

        modelBuilder.Entity<MovimientoCaja>(entity =>
        {
            entity.ToTable("MovimientosCaja");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Descripcion).IsRequired().HasMaxLength(200);
            entity.Property(m => m.PersonalRelacionado).HasMaxLength(150);
            entity.Property(m => m.Monto).HasPrecision(10, 2);
        });

        // ----------------------------------------------------------------
        // ALMACÉN / INVENTARIO
        // ----------------------------------------------------------------
        modelBuilder.Entity<Insumo>(entity =>
        {
            entity.ToTable("Insumos");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Nombre).IsRequired().HasMaxLength(100);
            entity.Property(i => i.UnidadMedida).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<MovimientoInventario>(entity =>
        {
            entity.ToTable("MovimientosInventario");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Motivo).IsRequired().HasMaxLength(200);
        });

        // ----------------------------------------------------------------
        // AUDITORÍA
        // ----------------------------------------------------------------
        modelBuilder.Entity<LogAuditoria>(entity =>
        {
            entity.ToTable("LogsAuditoria");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.TipoAccion).IsRequired().HasMaxLength(50);
            entity.Property(l => l.EntidadAfectada).IsRequired().HasMaxLength(50);
        });

        // ==================================================================
        // SEED: datos iniciales de demostración
        // ==================================================================
        SeedUsuarios(modelBuilder);
        SeedHabitaciones(modelBuilder);
        SeedProductosPOS(modelBuilder);
        SeedInsumos(modelBuilder);
    }

    private static void SeedUsuarios(ModelBuilder modelBuilder)
    {
        // ⚠️ ATENCIÓN ANTES DE INSTALAR EN EL HOTEL: estos 3 usuarios (incluido el
        // de rol Desarrollador, que tiene acceso total) se crean SIEMPRE la primera
        // vez que corre la app —también en una build Release— con la misma
        // contraseña "1234" para los tres. Hoy no existe pantalla de "cambiar
        // contraseña" en el sistema, así que hay que decidir cómo reemplazarla
        // antes de usar datos reales (a mano en la base, o construyendo esa
        // pantalla). No depender de esta contraseña por defecto en producción.
        const string hashPasswordDemo = "$2b$11$JucXsFC6/Xlkhh/qvHvjDejcGLdbOjdbfzyCbQEDTMJYxxWIf6Gf2";
        var fechaSeed = new DateTime(2026, 1, 1);

        modelBuilder.Entity<Usuario>().HasData(
            new Usuario { Id = 1, Username = "gerencia.1", NombreCompleto = "Gerencia", Rol = RolUsuario.Gerencia, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
            new Usuario { Id = 5, Username = "recepcion", NombreCompleto = "Recepción General", Rol = RolUsuario.Recepcionista, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
            new Usuario { Id = 6, Username = "marcelo.dev", NombreCompleto = "Marcelo López", Rol = RolUsuario.Desarrollador, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed }
        );
    }

    private static void SeedHabitaciones(ModelBuilder modelBuilder)
    {
        // Tarifas y tipos según especificación del negocio.
        // Piso 1-3: 9 habitaciones normales. Piso 4: 7 normales + 2 Suite (408 y 409).
        var habitaciones = new List<Habitacion>();
        int id = 1;

        (TipoHabitacion tipo, decimal tarifa, int capacidad) DatosPorNumeroEnPiso(int numeroEnPiso, int piso)
        {
            // Las 2 Suite van en el piso 4, habitaciones 408 y 409 (posiciones 8 y 9 del piso).
            if (piso == 4 && numeroEnPiso >= 8)
            {
                return (TipoHabitacion.Suite, 250m, 2);
            }

            // Distribución simple y predecible del resto de tipos dentro de cada piso.
            return (numeroEnPiso % 4) switch
            {
                1 => (TipoHabitacion.Simple, 120m, 1),
                2 => (TipoHabitacion.Matrimonial, 150m, 2),
                3 => (TipoHabitacion.Doble, 150m, 2),
                _ => (TipoHabitacion.Familiar, 190m, 4)
            };
        }

        for (int piso = 1; piso <= 4; piso++)
        {
            for (int numeroEnPiso = 1; numeroEnPiso <= 9; numeroEnPiso++)
            {
                var (tipo, tarifa, capacidad) = DatosPorNumeroEnPiso(numeroEnPiso, piso);

                habitaciones.Add(new Habitacion
                {
                    Id = id++,
                    Numero = piso * 100 + numeroEnPiso,
                    Piso = piso,
                    Tipo = tipo,
                    Estado = EstadoHabitacion.Disponible,
                    TarifaNoche = tarifa,
                    CapacidadMax = capacidad
                });
            }
        }

        modelBuilder.Entity<Habitacion>().HasData(habitaciones);
    }

    private static void SeedProductosPOS(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductoPOS>().HasData(
            // --- Recepción Sauna: alquiler o venta, a elección del usuario en el POS ---
            new ProductoPOS { Id = 1, Nombre = "Toalla", Precio = 0m, Categoria = CategoriaProducto.AlquilerSauna, Icono = "🏖️", RequiereDevolucion = true, Activo = true, EsAlquilerVenta = true, PrecioAlquiler = 5m, PrecioVenta = 20m },
            new ProductoPOS { Id = 2, Nombre = "Sandalias", Precio = 0m, Categoria = CategoriaProducto.AlquilerSauna, Icono = "🩴", RequiereDevolucion = true, Activo = true, EsAlquilerVenta = true, PrecioAlquiler = 5m, PrecioVenta = 25m },
            new ProductoPOS { Id = 3, Nombre = "Trapo exfoliante", Precio = 8m, Categoria = CategoriaProducto.VentaSauna, Icono = "🧽", RequiereDevolucion = false, Activo = true },
            new ProductoPOS { Id = 4, Nombre = "Lentes de natación", Precio = 15m, Categoria = CategoriaProducto.VentaSauna, Icono = "🥽", RequiereDevolucion = false, Activo = true },
            new ProductoPOS { Id = 15, Nombre = "Shorts", Precio = 0m, Categoria = CategoriaProducto.AlquilerSauna, Icono = "🩳", RequiereDevolucion = true, Activo = true, EsAlquilerVenta = true, PrecioAlquiler = 8m, PrecioVenta = 35m },

            // --- Cafetería: Bebidas ---
            new ProductoPOS { Id = 5, Nombre = "Gaseosa", Precio = 5m, Categoria = CategoriaProducto.BebidaCafeteria, Icono = "🥤", Activo = true },
            new ProductoPOS { Id = 6, Nombre = "Agua mineral", Precio = 3m, Categoria = CategoriaProducto.BebidaCafeteria, Icono = "💧", Activo = true },
            new ProductoPOS { Id = 7, Nombre = "Rehidratante", Precio = 6m, Categoria = CategoriaProducto.BebidaCafeteria, Icono = "🧃", Activo = true },
            new ProductoPOS { Id = 8, Nombre = "Jugo natural", Precio = 7m, Categoria = CategoriaProducto.BebidaCafeteria, Icono = "🍹", Activo = true },

            // --- Cafetería: Alimentos ---
            new ProductoPOS { Id = 9, Nombre = "Paleta", Precio = 4m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🍡", Activo = true },
            new ProductoPOS { Id = 10, Nombre = "Gelatina", Precio = 4m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🍮", Activo = true },
            new ProductoPOS { Id = 11, Nombre = "Keke", Precio = 6m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🍰", Activo = true },
            new ProductoPOS { Id = 12, Nombre = "Empanada", Precio = 6m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🥟", Activo = true },
            new ProductoPOS { Id = 13, Nombre = "Sánguche", Precio = 9m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🥪", Activo = true },
            new ProductoPOS { Id = 14, Nombre = "Ensalada", Precio = 12m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🥗", Activo = true },

            // --- Servicios adicionales de Hotel (Cafetería) ---
            new ProductoPOS { Id = 16, Nombre = "Planchado (prenda)", Precio = 5m, Categoria = CategoriaProducto.ServicioHotel, Icono = "👔", Activo = true },
            new ProductoPOS { Id = 17, Nombre = "Lavandería (kg)", Precio = 10m, Categoria = CategoriaProducto.ServicioHotel, Icono = "🧺", Activo = true },
            new ProductoPOS { Id = 18, Nombre = "Frazada adicional", Precio = 15m, Categoria = CategoriaProducto.ServicioHotel, Icono = "🛏️", Activo = true },
            new ProductoPOS { Id = 19, Nombre = "Toalla adicional", Precio = 10m, Categoria = CategoriaProducto.ServicioHotel, Icono = "🧻", Activo = true }
        );
    }

    /// <summary>Catálogo inicial de Almacén, según la lista específica del cliente:
    /// blancos de habitación, insumos de cocina, e insumos de sauna (como stock a
    /// reponer — distinto de ProductoPOS, que es lo que se le vende al cliente).</summary>
    private static void SeedInsumos(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Insumo>().HasData(
            // --- Hotel / Habitaciones ---
            new Insumo { Id = 1, Nombre = "Frazada de alpaca (antialérgica)", Categoria = CategoriaInsumo.HotelHabitaciones, UnidadMedida = "unidad", StockActual = 40, StockMinimo = 10, Activo = true },
            new Insumo { Id = 2, Nombre = "Frazada común", Categoria = CategoriaInsumo.HotelHabitaciones, UnidadMedida = "unidad", StockActual = 60, StockMinimo = 15, Activo = true },
            new Insumo { Id = 3, Nombre = "Cobertor de cama", Categoria = CategoriaInsumo.HotelHabitaciones, UnidadMedida = "unidad", StockActual = 50, StockMinimo = 12, Activo = true },
            new Insumo { Id = 4, Nombre = "Cobertor de almohada", Categoria = CategoriaInsumo.HotelHabitaciones, UnidadMedida = "unidad", StockActual = 80, StockMinimo = 20, Activo = true },

            // --- Hotel / Cocina ---
            new Insumo { Id = 5, Nombre = "Cubiertos (juego)", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "juego", StockActual = 100, StockMinimo = 20, Activo = true },
            new Insumo { Id = 6, Nombre = "Tazas", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 60, StockMinimo = 15, Activo = true },
            new Insumo { Id = 7, Nombre = "Platos", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 60, StockMinimo = 15, Activo = true },
            new Insumo { Id = 8, Nombre = "Huevos", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "docena", StockActual = 20, StockMinimo = 5, Activo = true },
            new Insumo { Id = 9, Nombre = "Mantequilla Horeca", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 15, StockMinimo = 5, Activo = true },
            new Insumo { Id = 10, Nombre = "Mermelada Horeca", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 15, StockMinimo = 5, Activo = true },
            new Insumo { Id = 11, Nombre = "Té filtrante", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "caja", StockActual = 30, StockMinimo = 8, Activo = true },
            new Insumo { Id = 12, Nombre = "Café en sobres", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "caja", StockActual = 30, StockMinimo = 8, Activo = true },
            new Insumo { Id = 13, Nombre = "Aceite", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "litro", StockActual = 20, StockMinimo = 5, Activo = true },
            new Insumo { Id = 14, Nombre = "Agua embotellada", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 100, StockMinimo = 20, Activo = true },
            new Insumo { Id = 15, Nombre = "Gaseosa (cocina)", Categoria = CategoriaInsumo.HotelCocina, UnidadMedida = "unidad", StockActual = 60, StockMinimo = 15, Activo = true },

            // --- Sauna (insumo de stock, no solo ítem de venta) ---
            new Insumo { Id = 16, Nombre = "Sandalias", Categoria = CategoriaInsumo.Sauna, UnidadMedida = "par", StockActual = 40, StockMinimo = 10, Activo = true },
            new Insumo { Id = 17, Nombre = "Candados", Categoria = CategoriaInsumo.Sauna, UnidadMedida = "unidad", StockActual = 50, StockMinimo = 10, Activo = true },
            new Insumo { Id = 18, Nombre = "Toallas", Categoria = CategoriaInsumo.Sauna, UnidadMedida = "unidad", StockActual = 80, StockMinimo = 20, Activo = true },
            new Insumo { Id = 19, Nombre = "Shorts", Categoria = CategoriaInsumo.Sauna, UnidadMedida = "unidad", StockActual = 40, StockMinimo = 10, Activo = true }
        );
    }
}
