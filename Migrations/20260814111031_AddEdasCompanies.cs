using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddEdasCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdasCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdasCompanies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdasCompanies_Name",
                table: "EdasCompanies",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            var seedTime = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
            var seedNames = new[]
            {
                "VANGÖLÜ", "ULUDAĞ", "TIRAKYA", "TOROSLAR", "SAKARYA", "OSMANGAZİ", "MERAM", "KCTAŞ",
                "GDZ", "FIRAT", "DİCLE", "ÇORUH", "ÇAMLIBEL", "BOĞAZİÇİ", "BAŞKENT", "AYEDAŞ", "AKEDAŞ",
                "AKDENİZ", "ADM", "ARAS",
            };

            for (var i = 0; i < seedNames.Length; i++)
            {
                migrationBuilder.InsertData(
                    table: "EdasCompanies",
                    columns: new[] { "Id", "Name", "CreatedAt", "UpdatedAt", "IsDeleted" },
                    values: new object[]
                    {
                        new Guid($"a1000000-0000-4000-8000-{(i + 1):D12}"),
                        seedNames[i],
                        seedTime,
                        null,
                        false,
                    });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdasCompanies");
        }
    }
}
