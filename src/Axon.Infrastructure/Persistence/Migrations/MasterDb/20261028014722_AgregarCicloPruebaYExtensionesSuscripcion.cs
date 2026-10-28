using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Infrastructure.Persistence.Migrations.MasterDb
{
    /// <inheritdoc />
    public partial class AgregarCicloPruebaYExtensionesSuscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "billing_cycle",
                schema: "public",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_trial",
                schema: "public",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "subscription_extensions",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_cycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    extension_days = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    transaction_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    previous_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    new_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscription_extensions", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscription_extensions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "public",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_extensions_tenant_id_created_at",
                schema: "public",
                table: "subscription_extensions",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_subscription_extensions_transaction_reference",
                schema: "public",
                table: "subscription_extensions",
                column: "transaction_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "subscription_extensions",
                schema: "public");

            migrationBuilder.DropColumn(
                name: "billing_cycle",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "is_trial",
                schema: "public",
                table: "tenants");
        }
    }
}
