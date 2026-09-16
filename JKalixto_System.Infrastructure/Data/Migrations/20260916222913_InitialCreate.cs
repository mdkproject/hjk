using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JKalixto_System.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CierresCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Turno = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalHotel = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    TotalSauna = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    FechaCierre = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CierresCaja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientesSauna",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TipoDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", nullable: false),
                    NumeroCandado = table.Column<string>(type: "TEXT", nullable: false),
                    Seccion = table.Column<int>(type: "INTEGER", nullable: false),
                    Observacion = table.Column<string>(type: "TEXT", nullable: true),
                    FechaIngreso = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaSalida = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    EsHuespedHotel = table.Column<bool>(type: "INTEGER", nullable: false),
                    EstadiaHotelId = table.Column<int>(type: "INTEGER", nullable: true),
                    TipoComprobante = table.Column<int>(type: "INTEGER", nullable: false),
                    RUC = table.Column<string>(type: "TEXT", nullable: true),
                    RazonSocial = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientesSauna", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Habitaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Numero = table.Column<int>(type: "INTEGER", nullable: false),
                    Piso = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    TarifaNoche = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    CapacidadMax = table.Column<int>(type: "INTEGER", nullable: false),
                    MotivoMantenimiento = table.Column<string>(type: "TEXT", nullable: true),
                    FechaInicioMantenimiento = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Habitaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insumos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Categoria = table.Column<int>(type: "INTEGER", nullable: false),
                    UnidadMedida = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StockActual = table.Column<int>(type: "INTEGER", nullable: false),
                    StockMinimo = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insumos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogsAuditoria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TipoAccion = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "TEXT", nullable: false),
                    EntidadAfectada = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EntidadId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsAuditoria", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FechaHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Direccion = table.Column<int>(type: "INTEGER", nullable: false),
                    Categoria = table.Column<int>(type: "INTEGER", nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PersonalRelacionado = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    Monto = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    OrigenCaja = table.Column<int>(type: "INTEGER", nullable: false),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InsumoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FechaHora = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumeracionesComprobante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Serie = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    UltimoCorrelativo = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumeracionesComprobante", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Penalidades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Monto = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Motivo = table.Column<string>(type: "TEXT", nullable: false),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClienteSaunaId = table.Column<int>(type: "INTEGER", nullable: true),
                    EstadiaHotelId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Penalidades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductosPOS",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", nullable: false),
                    Precio = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Categoria = table.Column<int>(type: "INTEGER", nullable: false),
                    Icono = table.Column<string>(type: "TEXT", nullable: false),
                    RequiereDevolucion = table.Column<bool>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    EsAlquilerVenta = table.Column<bool>(type: "INTEGER", nullable: false),
                    PrecioAlquiler = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    PrecioVenta = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosPOS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reclamos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Domicilio = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    TipoDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Telefono = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    EsMenorDeEdad = table.Column<bool>(type: "INTEGER", nullable: false),
                    NombreApoderado = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    DocumentoApoderado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    BienContratado = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MontoReclamado = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    DetalleReclamo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    PedidoConsumidor = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    RespuestaEstablecimiento = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FechaRespuesta = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioRegistroId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reclamos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Rol = table.Column<int>(type: "INTEGER", nullable: false),
                    Activo = table.Column<bool>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VentasSauna",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClienteSaunaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Fecha = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Total = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadiaHotelDestinoId = table.Column<int>(type: "INTEGER", nullable: true),
                    UsuarioId = table.Column<int>(type: "INTEGER", nullable: false),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: true),
                    NumeroComprobante = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VentasSauna", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Estadias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HabitacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Celular = table.Column<string>(type: "TEXT", nullable: false),
                    FechaNacimiento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Sexo = table.Column<int>(type: "INTEGER", nullable: true),
                    Nacionalidad = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LugarResidencia = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    MotivoViaje = table.Column<int>(type: "INTEGER", nullable: true),
                    FechaCheckIn = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaCheckOut = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoComprobante = table.Column<int>(type: "INTEGER", nullable: false),
                    RUC = table.Column<string>(type: "TEXT", nullable: true),
                    RazonSocial = table.Column<string>(type: "TEXT", nullable: true),
                    CorreoFacturacion = table.Column<string>(type: "TEXT", nullable: true),
                    NumeroComprobante = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    MetodoPago = table.Column<int>(type: "INTEGER", nullable: true),
                    AccesoSaunaIncluido = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalAcumulado = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    UsuarioCheckInId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioCheckOutId = table.Column<int>(type: "INTEGER", nullable: true),
                    Observaciones = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Estadias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Estadias_Habitaciones_HabitacionId",
                        column: x => x.HabitacionId,
                        principalTable: "Habitaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HabitacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoDocumento = table.Column<int>(type: "INTEGER", nullable: false),
                    NumeroDocumento = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Celular = table.Column<string>(type: "TEXT", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Estado = table.Column<int>(type: "INTEGER", nullable: false),
                    Observaciones = table.Column<string>(type: "TEXT", nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsuarioCreacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    EstadiaId = table.Column<int>(type: "INTEGER", nullable: true),
                    TipoComprobante = table.Column<int>(type: "INTEGER", nullable: false),
                    RUC = table.Column<string>(type: "TEXT", nullable: true),
                    RazonSocial = table.Column<string>(type: "TEXT", nullable: true),
                    CorreoFacturacion = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservas_Habitaciones_HabitacionId",
                        column: x => x.HabitacionId,
                        principalTable: "Habitaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    VentaSaunaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductoId = table.Column<int>(type: "INTEGER", nullable: true),
                    Descripcion = table.Column<string>(type: "TEXT", nullable: false),
                    Cantidad = table.Column<int>(type: "INTEGER", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesVenta_VentasSauna_VentaSaunaId",
                        column: x => x.VentaSaunaId,
                        principalTable: "VentasSauna",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acompanantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EstadiaId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acompanantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acompanantes_Estadias_EstadiaId",
                        column: x => x.EstadiaId,
                        principalTable: "Estadias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcompanantesReserva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReservaId = table.Column<int>(type: "INTEGER", nullable: false),
                    NombreCompleto = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcompanantesReserva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcompanantesReserva_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Habitaciones",
                columns: new[] { "Id", "CapacidadMax", "Estado", "FechaInicioMantenimiento", "MotivoMantenimiento", "Numero", "Piso", "TarifaNoche", "Tipo" },
                values: new object[,]
                {
                    { 1, 1, 0, null, null, 101, 1, 120m, 0 },
                    { 2, 2, 0, null, null, 102, 1, 150m, 1 },
                    { 3, 2, 0, null, null, 103, 1, 150m, 2 },
                    { 4, 4, 0, null, null, 104, 1, 190m, 3 },
                    { 5, 1, 0, null, null, 105, 1, 120m, 0 },
                    { 6, 2, 0, null, null, 106, 1, 150m, 1 },
                    { 7, 2, 0, null, null, 107, 1, 150m, 2 },
                    { 8, 4, 0, null, null, 108, 1, 190m, 3 },
                    { 9, 1, 0, null, null, 109, 1, 120m, 0 },
                    { 10, 1, 0, null, null, 201, 2, 120m, 0 },
                    { 11, 2, 0, null, null, 202, 2, 150m, 1 },
                    { 12, 2, 0, null, null, 203, 2, 150m, 2 },
                    { 13, 4, 0, null, null, 204, 2, 190m, 3 },
                    { 14, 1, 0, null, null, 205, 2, 120m, 0 },
                    { 15, 2, 0, null, null, 206, 2, 150m, 1 },
                    { 16, 2, 0, null, null, 207, 2, 150m, 2 },
                    { 17, 4, 0, null, null, 208, 2, 190m, 3 },
                    { 18, 1, 0, null, null, 209, 2, 120m, 0 },
                    { 19, 1, 0, null, null, 301, 3, 120m, 0 },
                    { 20, 2, 0, null, null, 302, 3, 150m, 1 },
                    { 21, 2, 0, null, null, 303, 3, 150m, 2 },
                    { 22, 4, 0, null, null, 304, 3, 190m, 3 },
                    { 23, 1, 0, null, null, 305, 3, 120m, 0 },
                    { 24, 2, 0, null, null, 306, 3, 150m, 1 },
                    { 25, 2, 0, null, null, 307, 3, 150m, 2 },
                    { 26, 4, 0, null, null, 308, 3, 190m, 3 },
                    { 27, 1, 0, null, null, 309, 3, 120m, 0 },
                    { 28, 1, 0, null, null, 401, 4, 120m, 0 },
                    { 29, 2, 0, null, null, 402, 4, 150m, 1 },
                    { 30, 2, 0, null, null, 403, 4, 150m, 2 },
                    { 31, 4, 0, null, null, 404, 4, 190m, 3 },
                    { 32, 1, 0, null, null, 405, 4, 120m, 0 },
                    { 33, 2, 0, null, null, 406, 4, 150m, 1 },
                    { 34, 2, 0, null, null, 407, 4, 150m, 2 },
                    { 35, 2, 0, null, null, 408, 4, 250m, 4 },
                    { 36, 2, 0, null, null, 409, 4, 250m, 4 }
                });

            migrationBuilder.InsertData(
                table: "Insumos",
                columns: new[] { "Id", "Activo", "Categoria", "Nombre", "StockActual", "StockMinimo", "UnidadMedida" },
                values: new object[,]
                {
                    { 1, true, 0, "Frazada de alpaca (antialérgica)", 40, 10, "unidad" },
                    { 2, true, 0, "Frazada común", 60, 15, "unidad" },
                    { 3, true, 0, "Cobertor de cama", 50, 12, "unidad" },
                    { 4, true, 0, "Cobertor de almohada", 80, 20, "unidad" },
                    { 5, true, 1, "Cubiertos (juego)", 100, 20, "juego" },
                    { 6, true, 1, "Tazas", 60, 15, "unidad" },
                    { 7, true, 1, "Platos", 60, 15, "unidad" },
                    { 8, true, 1, "Huevos", 20, 5, "docena" },
                    { 9, true, 1, "Mantequilla Horeca", 15, 5, "unidad" },
                    { 10, true, 1, "Mermelada Horeca", 15, 5, "unidad" },
                    { 11, true, 1, "Té filtrante", 30, 8, "caja" },
                    { 12, true, 1, "Café en sobres", 30, 8, "caja" },
                    { 13, true, 1, "Aceite", 20, 5, "litro" },
                    { 14, true, 1, "Agua embotellada", 100, 20, "unidad" },
                    { 15, true, 1, "Gaseosa (cocina)", 60, 15, "unidad" },
                    { 16, true, 2, "Sandalias", 40, 10, "par" },
                    { 17, true, 2, "Candados", 50, 10, "unidad" },
                    { 18, true, 2, "Toallas", 80, 20, "unidad" },
                    { 19, true, 2, "Shorts", 40, 10, "unidad" }
                });

            migrationBuilder.InsertData(
                table: "ProductosPOS",
                columns: new[] { "Id", "Activo", "Categoria", "EsAlquilerVenta", "Icono", "Nombre", "Precio", "PrecioAlquiler", "PrecioVenta", "RequiereDevolucion" },
                values: new object[,]
                {
                    { 1, true, 0, true, "🏖️", "Toalla", 0m, 5m, 20m, true },
                    { 2, true, 0, true, "🩴", "Sandalias", 0m, 5m, 25m, true },
                    { 3, true, 1, false, "🧽", "Trapo exfoliante", 8m, 0m, 0m, false },
                    { 4, true, 1, false, "🥽", "Lentes de natación", 15m, 0m, 0m, false },
                    { 5, true, 2, false, "🥤", "Gaseosa", 5m, 0m, 0m, false },
                    { 6, true, 2, false, "💧", "Agua mineral", 3m, 0m, 0m, false },
                    { 7, true, 2, false, "🧃", "Rehidratante", 6m, 0m, 0m, false },
                    { 8, true, 2, false, "🍹", "Jugo natural", 7m, 0m, 0m, false },
                    { 9, true, 3, false, "🍡", "Paleta", 4m, 0m, 0m, false },
                    { 10, true, 3, false, "🍮", "Gelatina", 4m, 0m, 0m, false },
                    { 11, true, 3, false, "🍰", "Keke", 6m, 0m, 0m, false },
                    { 12, true, 3, false, "🥟", "Empanada", 6m, 0m, 0m, false },
                    { 13, true, 3, false, "🥪", "Sánguche", 9m, 0m, 0m, false },
                    { 14, true, 3, false, "🥗", "Ensalada", 12m, 0m, 0m, false },
                    { 15, true, 0, true, "🩳", "Shorts", 0m, 8m, 35m, true },
                    { 16, true, 5, false, "👔", "Planchado (prenda)", 5m, 0m, 0m, false },
                    { 17, true, 5, false, "🧺", "Lavandería (kg)", 10m, 0m, 0m, false },
                    { 18, true, 5, false, "🛏️", "Frazada adicional", 15m, 0m, 0m, false },
                    { 19, true, 5, false, "🧻", "Toalla adicional", 10m, 0m, 0m, false }
                });

            migrationBuilder.InsertData(
                table: "Usuarios",
                columns: new[] { "Id", "Activo", "FechaCreacion", "NombreCompleto", "PasswordHash", "Rol", "Username" },
                values: new object[,]
                {
                    { 1, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Gerencia", "$2b$11$JucXsFC6/Xlkhh/qvHvjDejcGLdbOjdbfzyCbQEDTMJYxxWIf6Gf2", 0, "gerencia.1" },
                    { 5, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Recepción General", "$2b$11$JucXsFC6/Xlkhh/qvHvjDejcGLdbOjdbfzyCbQEDTMJYxxWIf6Gf2", 1, "recepcion" },
                    { 6, true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "Marcelo López", "$2b$11$JucXsFC6/Xlkhh/qvHvjDejcGLdbOjdbfzyCbQEDTMJYxxWIf6Gf2", 2, "marcelo.dev" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acompanantes_EstadiaId",
                table: "Acompanantes",
                column: "EstadiaId");

            migrationBuilder.CreateIndex(
                name: "IX_AcompanantesReserva_ReservaId",
                table: "AcompanantesReserva",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_VentaSaunaId",
                table: "DetallesVenta",
                column: "VentaSaunaId");

            migrationBuilder.CreateIndex(
                name: "IX_Estadias_HabitacionId",
                table: "Estadias",
                column: "HabitacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Habitaciones_Numero",
                table: "Habitaciones",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumeracionesComprobante_Tipo_Serie",
                table: "NumeracionesComprobante",
                columns: new[] { "Tipo", "Serie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_HabitacionId",
                table: "Reservas",
                column: "HabitacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Username",
                table: "Usuarios",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acompanantes");

            migrationBuilder.DropTable(
                name: "AcompanantesReserva");

            migrationBuilder.DropTable(
                name: "CierresCaja");

            migrationBuilder.DropTable(
                name: "ClientesSauna");

            migrationBuilder.DropTable(
                name: "DetallesVenta");

            migrationBuilder.DropTable(
                name: "Insumos");

            migrationBuilder.DropTable(
                name: "LogsAuditoria");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "NumeracionesComprobante");

            migrationBuilder.DropTable(
                name: "Penalidades");

            migrationBuilder.DropTable(
                name: "ProductosPOS");

            migrationBuilder.DropTable(
                name: "Reclamos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Estadias");

            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropTable(
                name: "VentasSauna");

            migrationBuilder.DropTable(
                name: "Habitaciones");
        }
    }
}
