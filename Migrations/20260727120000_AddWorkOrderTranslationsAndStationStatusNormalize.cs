using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderTranslationsAndStationStatusNormalize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MobileDescriptionEn",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FieldNoteEn",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationProvider",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TranslatedAt",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            // Geçici politika: tüm istasyon durumlarını Bakıma Dahil yap
            migrationBuilder.Sql(
                """
                UPDATE public."Stations"
                SET "StatusType" = 'Bakıma Dahil'
                WHERE "IsDeleted" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TitleEn", table: "WorkOrders");
            migrationBuilder.DropColumn(name: "DescriptionEn", table: "WorkOrders");
            migrationBuilder.DropColumn(name: "MobileDescriptionEn", table: "WorkOrders");
            migrationBuilder.DropColumn(name: "FieldNoteEn", table: "WorkOrders");
            migrationBuilder.DropColumn(name: "TranslationProvider", table: "WorkOrders");
            migrationBuilder.DropColumn(name: "TranslatedAt", table: "WorkOrders");
        }
    }
}
