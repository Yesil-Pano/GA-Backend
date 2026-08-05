using GA.Application.Features.Partners;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPartnerKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PartnerKey",
                table: "Tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PartnerKey",
                table: "Tenants",
                column: "PartnerKey",
                unique: true,
                filter: "\"PartnerKey\" IS NOT NULL");

            var trugoId = PartnerCatalog.SeedTenantIds.Trugo;
            var yesilPanoId = PartnerCatalog.SeedTenantIds.YesilPano;
            var teslaId = PartnerCatalog.SeedTenantIds.Tesla;
            var astorId = PartnerCatalog.SeedTenantIds.Astor;
            var now = DateTime.UtcNow.ToString("O");

            migrationBuilder.Sql($"""
                UPDATE "Tenants"
                SET "PartnerKey" = 'trugo', "UpdatedAt" = '{now}'
                WHERE "Id" = '{trugoId}' AND ("PartnerKey" IS NULL OR "PartnerKey" = '');

                UPDATE "Tenants"
                SET "PartnerKey" = 'yesilpano', "UpdatedAt" = '{now}'
                WHERE "Id" = '{yesilPanoId}' AND ("PartnerKey" IS NULL OR "PartnerKey" = '');

                INSERT INTO "Tenants" ("Id", "Name", "TaxNumber", "IsActive", "IsDemo", "DemoExpiresAt", "PartnerKey", "CreatedAt", "UpdatedAt", "IsDeleted")
                SELECT '{teslaId}', 'TESLA', NULL, TRUE, FALSE, NULL, 'tesla', '{now}', NULL, FALSE
                WHERE NOT EXISTS (SELECT 1 FROM "Tenants" WHERE "PartnerKey" = 'tesla');

                INSERT INTO "Tenants" ("Id", "Name", "TaxNumber", "IsActive", "IsDemo", "DemoExpiresAt", "PartnerKey", "CreatedAt", "UpdatedAt", "IsDeleted")
                SELECT '{astorId}', 'Astor Enerji', NULL, TRUE, FALSE, NULL, 'astor', '{now}', NULL, FALSE
                WHERE NOT EXISTS (SELECT 1 FROM "Tenants" WHERE "PartnerKey" = 'astor');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var teslaId = PartnerCatalog.SeedTenantIds.Tesla;
            var astorId = PartnerCatalog.SeedTenantIds.Astor;

            migrationBuilder.Sql($"""
                DELETE FROM "Tenants" WHERE "Id" IN ('{teslaId}', '{astorId}') AND "PartnerKey" IN ('tesla', 'astor');
                UPDATE "Tenants" SET "PartnerKey" = NULL WHERE "PartnerKey" IN ('trugo', 'yesilpano', 'tesla', 'astor');
                """);

            migrationBuilder.DropIndex(
                name: "IX_Tenants_PartnerKey",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PartnerKey",
                table: "Tenants");
        }
    }
}
