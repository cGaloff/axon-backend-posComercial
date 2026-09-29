using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Infrastructure.Persistence.Migrations.MasterDb
{
    /// <inheritdoc />
    public partial class AgregarVencimientoSuscripcionTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "subscription_ends_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "subscription_ends_at",
                schema: "public",
                table: "tenants");
        }
    }
}
