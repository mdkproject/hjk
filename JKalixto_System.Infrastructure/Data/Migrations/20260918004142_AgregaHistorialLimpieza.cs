using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JKalixto_System.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregaHistorialLimpieza : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrosLimpieza",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HabitacionId = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaFin = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UsuarioInicioId = table.Column<int>(type: "INTEGER", nullable: false),
                    UsuarioFinId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosLimpieza", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosLimpieza_Habitaciones_HabitacionId",
                        column: x => x.HabitacionId,
                        principalTable: "Habitaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosLimpieza_Usuarios_UsuarioFinId",
                        column: x => x.UsuarioFinId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RegistrosLimpieza_Usuarios_UsuarioInicioId",
                        column: x => x.UsuarioInicioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosLimpieza_HabitacionId",
                table: "RegistrosLimpieza",
                column: "HabitacionId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosLimpieza_UsuarioFinId",
                table: "RegistrosLimpieza",
                column: "UsuarioFinId");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosLimpieza_UsuarioInicioId",
                table: "RegistrosLimpieza",
                column: "UsuarioInicioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegistrosLimpieza");
        }
    }
}
