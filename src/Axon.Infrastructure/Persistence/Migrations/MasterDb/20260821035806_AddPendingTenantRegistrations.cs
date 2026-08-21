using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Infrastructure.Persistence.Migrations.MasterDb
{
    /// <inheritdoc />
    public partial class AddPendingTenantRegistrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_tenant_registrations",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    owner_password_hash = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pending_tenant_registrations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pending_tenant_registrations_owner_email",
                schema: "public",
                table: "pending_tenant_registrations",
                column: "owner_email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_tenant_registrations",
                schema: "public");
        }
    }
}
