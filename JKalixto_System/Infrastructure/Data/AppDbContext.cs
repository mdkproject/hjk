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
    public DbSet<Acompanante> Acompanantes => Set<Acompanante>();
    public DbSet<ClienteSauna> ClientesSauna => Set<ClienteSauna>();
    public DbSet<ProductoPOS> ProductosPOS => Set<ProductoPOS>();
    public DbSet<VentaSauna> VentasSauna => Set<VentaSauna>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<Penalidad> Penalidades => Set<Penalidad>();
    public DbSet<CierreCaja> CierresCaja => Set<CierreCaja>();
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
        });

        // ----------------------------------------------------------------
        // ESTADIAS + ACOMPAÑANTES
        // ----------------------------------------------------------------
        modelBuilder.Entity<Estadia>(entity =>
        {
            entity.ToTable("Estadias");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DNI).IsRequired().HasMaxLength(15);
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
    }

    private static void SeedUsuarios(ModelBuilder modelBuilder)
    {
        // Contraseña real para TODOS: "1234" (ya encriptada con BCrypt).
        // Solo para desarrollo/demo — cambiar antes de producción.
        const string hashPasswordDemo = "$2b$11$JucXsFC6/Xlkhh/qvHvjDejcGLdbOjdbfzyCbQEDTMJYxxWIf6Gf2";
        var fechaSeed = new DateTime(2026, 1, 1);

        modelBuilder.Entity<Usuario>().HasData(
            new Usuario { Id = 1, Username = "lilian.chacon", NombreCompleto = "Lilian Chacón", Rol = RolUsuario.Gerencia, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
            new Usuario { Id = 2, Username = "gloria.chacon", NombreCompleto = "Gloria Chacón", Rol = RolUsuario.Gerencia, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
            new Usuario { Id = 3, Username = "miriam.chacon", NombreCompleto = "Miriam Chacón", Rol = RolUsuario.Gerencia, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
            new Usuario { Id = 4, Username = "marisela.chacon", NombreCompleto = "Marisela Chacón", Rol = RolUsuario.Gerencia, PasswordHash = hashPasswordDemo, Activo = true, FechaCreacion = fechaSeed },
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
            // --- Recepción Sauna ---
            new ProductoPOS { Id = 1, Nombre = "Alquiler de toalla", Precio = 5m, Categoria = CategoriaProducto.AlquilerSauna, Icono = "🏖️", RequiereDevolucion = true, Activo = true },
            new ProductoPOS { Id = 2, Nombre = "Alquiler de sandalias", Precio = 5m, Categoria = CategoriaProducto.AlquilerSauna, Icono = "🩴", RequiereDevolucion = true, Activo = true },
            new ProductoPOS { Id = 3, Nombre = "Trapo exfoliante", Precio = 8m, Categoria = CategoriaProducto.VentaSauna, Icono = "🧽", RequiereDevolucion = false, Activo = true },
            new ProductoPOS { Id = 4, Nombre = "Lentes de natación", Precio = 15m, Categoria = CategoriaProducto.VentaSauna, Icono = "🥽", RequiereDevolucion = false, Activo = true },

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
            new ProductoPOS { Id = 14, Nombre = "Ensalada", Precio = 12m, Categoria = CategoriaProducto.AlimentoCafeteria, Icono = "🥗", Activo = true }
        );
    }
}
