using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Infrastructure.Persistence.Migrations.MasterDb
{
    /// <inheritdoc />
    public partial class UnificarVencimientoSuscripcionTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Prod (VPS) guardó el vencimiento en subscription_ends_at y develop
            // en subscription_expires_at. Se conserva la de develop; antes de
            // borrar la vieja se copian sus fechas para no dejar sin corte a los
            // tenants que ya lo tenían.
            migrationBuilder.Sql(
                "UPDATE public.tenants " +
                "SET subscription_expires_at = subscription_ends_at " +
                "WHERE subscription_expires_at IS NULL AND subscription_ends_at IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "subscription_ends_at",
                schema: "public",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "subscription_ends_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE public.tenants SET subscription_ends_at = subscription_expires_at;");
        }
    }
}
