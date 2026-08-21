using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Axon.Infrastructure.Persistence.Migrations.MasterDb
{
    /// <inheritdoc />
    public partial class AddTenantSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_trial_reminder_sent_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owner_email",
                schema: "public",
                table: "tenants",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "subscription_expires_at",
                schema: "public",
                table: "tenants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_trial_reminder_sent_at",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "owner_email",
                schema: "public",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "subscription_expires_at",
                schema: "public",
                table: "tenants");
        }
    }
}
