using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JKalixto_System.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AgregaClavesForaneas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VentasSauna_ClienteSaunaId",
                table: "VentasSauna",
                column: "ClienteSaunaId");

            migrationBuilder.CreateIndex(
                name: "IX_VentasSauna_EstadiaHotelDestinoId",
                table: "VentasSauna",
                column: "EstadiaHotelDestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_VentasSauna_UsuarioId",
                table: "VentasSauna",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_UsuarioCreacionId",
                table: "Reservas",
                column: "UsuarioCreacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Reclamos_UsuarioRegistroId",
                table: "Reclamos",
                column: "UsuarioRegistroId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalidades_ClienteSaunaId",
                table: "Penalidades",
                column: "ClienteSaunaId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalidades_EstadiaHotelId",
                table: "Penalidades",
                column: "EstadiaHotelId");

            migrationBuilder.CreateIndex(
                name: "IX_Penalidades_UsuarioId",
                table: "Penalidades",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_InsumoId",
                table: "MovimientosInventario",
                column: "InsumoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_UsuarioId",
                table: "MovimientosInventario",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_UsuarioId",
                table: "MovimientosCaja",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Estadias_UsuarioCheckInId",
                table: "Estadias",
                column: "UsuarioCheckInId");

            migrationBuilder.CreateIndex(
                name: "IX_Estadias_UsuarioCheckOutId",
                table: "Estadias",
                column: "UsuarioCheckOutId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_ProductoId",
                table: "DetallesVenta",
                column: "ProductoId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientesSauna_EstadiaHotelId",
                table: "ClientesSauna",
                column: "EstadiaHotelId");

            migrationBuilder.CreateIndex(
                name: "IX_CierresCaja_UsuarioId",
                table: "CierresCaja",
                column: "UsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_CierresCaja_Usuarios_UsuarioId",
                table: "CierresCaja",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientesSauna_Estadias_EstadiaHotelId",
                table: "ClientesSauna",
                column: "EstadiaHotelId",
                principalTable: "Estadias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesVenta_ProductosPOS_ProductoId",
                table: "DetallesVenta",
                column: "ProductoId",
                principalTable: "ProductosPOS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Estadias_Usuarios_UsuarioCheckInId",
                table: "Estadias",
                column: "UsuarioCheckInId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Estadias_Usuarios_UsuarioCheckOutId",
                table: "Estadias",
                column: "UsuarioCheckOutId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LogsAuditoria_Usuarios_UsuarioId",
                table: "LogsAuditoria",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_Usuarios_UsuarioId",
                table: "MovimientosCaja",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Insumos_InsumoId",
                table: "MovimientosInventario",
                column: "InsumoId",
                principalTable: "Insumos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosInventario_Usuarios_UsuarioId",
                table: "MovimientosInventario",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Penalidades_ClientesSauna_ClienteSaunaId",
                table: "Penalidades",
                column: "ClienteSaunaId",
                principalTable: "ClientesSauna",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Penalidades_Estadias_EstadiaHotelId",
                table: "Penalidades",
                column: "EstadiaHotelId",
                principalTable: "Estadias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Penalidades_Usuarios_UsuarioId",
                table: "Penalidades",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reclamos_Usuarios_UsuarioRegistroId",
                table: "Reclamos",
                column: "UsuarioRegistroId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservas_Usuarios_UsuarioCreacionId",
                table: "Reservas",
                column: "UsuarioCreacionId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VentasSauna_ClientesSauna_ClienteSaunaId",
                table: "VentasSauna",
                column: "ClienteSaunaId",
                principalTable: "ClientesSauna",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VentasSauna_Estadias_EstadiaHotelDestinoId",
                table: "VentasSauna",
                column: "EstadiaHotelDestinoId",
                principalTable: "Estadias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_VentasSauna_Usuarios_UsuarioId",
                table: "VentasSauna",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CierresCaja_Usuarios_UsuarioId",
                table: "CierresCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientesSauna_Estadias_EstadiaHotelId",
                table: "ClientesSauna");

            migrationBuilder.DropForeignKey(
                name: "FK_DetallesVenta_ProductosPOS_ProductoId",
                table: "DetallesVenta");

            migrationBuilder.DropForeignKey(
                name: "FK_Estadias_Usuarios_UsuarioCheckInId",
                table: "Estadias");

            migrationBuilder.DropForeignKey(
                name: "FK_Estadias_Usuarios_UsuarioCheckOutId",
                table: "Estadias");

            migrationBuilder.DropForeignKey(
                name: "FK_LogsAuditoria_Usuarios_UsuarioId",
                table: "LogsAuditoria");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_Usuarios_UsuarioId",
                table: "MovimientosCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_Insumos_InsumoId",
                table: "MovimientosInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosInventario_Usuarios_UsuarioId",
                table: "MovimientosInventario");

            migrationBuilder.DropForeignKey(
                name: "FK_Penalidades_ClientesSauna_ClienteSaunaId",
                table: "Penalidades");

            migrationBuilder.DropForeignKey(
                name: "FK_Penalidades_Estadias_EstadiaHotelId",
                table: "Penalidades");

            migrationBuilder.DropForeignKey(
                name: "FK_Penalidades_Usuarios_UsuarioId",
                table: "Penalidades");

            migrationBuilder.DropForeignKey(
                name: "FK_Reclamos_Usuarios_UsuarioRegistroId",
                table: "Reclamos");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservas_Usuarios_UsuarioCreacionId",
                table: "Reservas");

            migrationBuilder.DropForeignKey(
                name: "FK_VentasSauna_ClientesSauna_ClienteSaunaId",
                table: "VentasSauna");

            migrationBuilder.DropForeignKey(
                name: "FK_VentasSauna_Estadias_EstadiaHotelDestinoId",
                table: "VentasSauna");

            migrationBuilder.DropForeignKey(
                name: "FK_VentasSauna_Usuarios_UsuarioId",
                table: "VentasSauna");

            migrationBuilder.DropIndex(
                name: "IX_VentasSauna_ClienteSaunaId",
                table: "VentasSauna");

            migrationBuilder.DropIndex(
                name: "IX_VentasSauna_EstadiaHotelDestinoId",
                table: "VentasSauna");

            migrationBuilder.DropIndex(
                name: "IX_VentasSauna_UsuarioId",
                table: "VentasSauna");

            migrationBuilder.DropIndex(
                name: "IX_Reservas_UsuarioCreacionId",
                table: "Reservas");

            migrationBuilder.DropIndex(
                name: "IX_Reclamos_UsuarioRegistroId",
                table: "Reclamos");

            migrationBuilder.DropIndex(
                name: "IX_Penalidades_ClienteSaunaId",
                table: "Penalidades");

            migrationBuilder.DropIndex(
                name: "IX_Penalidades_EstadiaHotelId",
                table: "Penalidades");

            migrationBuilder.DropIndex(
                name: "IX_Penalidades_UsuarioId",
                table: "Penalidades");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_InsumoId",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosInventario_UsuarioId",
                table: "MovimientosInventario");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_UsuarioId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_LogsAuditoria_UsuarioId",
                table: "LogsAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_Estadias_UsuarioCheckInId",
                table: "Estadias");

            migrationBuilder.DropIndex(
                name: "IX_Estadias_UsuarioCheckOutId",
                table: "Estadias");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_ProductoId",
                table: "DetallesVenta");

            migrationBuilder.DropIndex(
                name: "IX_ClientesSauna_EstadiaHotelId",
                table: "ClientesSauna");

            migrationBuilder.DropIndex(
                name: "IX_CierresCaja_UsuarioId",
                table: "CierresCaja");
        }
    }
}
