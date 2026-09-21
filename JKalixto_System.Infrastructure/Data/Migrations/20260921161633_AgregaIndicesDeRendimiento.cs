using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JKalixto_System.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregaIndicesDeRendimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservas_HabitacionId",
                table: "Reservas");

            migrationBuilder.CreateIndex(
                name: "IX_VentasSauna_Fecha",
                table: "VentasSauna",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_HabitacionId_FechaInicio_FechaFin",
                table: "Reservas",
                columns: new[] { "HabitacionId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_FechaHora",
                table: "MovimientosCaja",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_Timestamp",
                table: "LogsAuditoria",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Estadias_Estado",
                table: "Estadias",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Estadias_FechaCheckOut",
                table: "Estadias",
                column: "FechaCheckOut");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VentasSauna_Fecha",
                table: "VentasSauna");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_HabitacionId_FechaInicio_FechaFin",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_FechaHora",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_Timestamp",
                table: "LogsAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_Estadias_Estado",
                table: "Estadias");

            migrationBuilder.DropIndex(
                name: "IX_Estadias_FechaCheckOut",
                table: "Estadias");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_HabitacionId",
                table: "Reservas",
                column: "HabitacionId");
        }
    }
}
