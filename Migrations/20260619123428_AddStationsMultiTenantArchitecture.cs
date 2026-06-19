using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddStationsMultiTenantArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StatusType = table.Column<string>(type: "text", nullable: false),
                    PowerType = table.Column<string>(type: "text", nullable: false),
                    PersonnelName = table.Column<string>(type: "text", nullable: false),
                    PersonnelPhone = table.Column<string>(type: "text", nullable: false),
                    Edas = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    PointType = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    GeneralFilePath = table.Column<string>(type: "text", nullable: true),
                    YgTescilBelgesiPath = table.Column<string>(type: "text", nullable: true),
                    YgSozlesmesiPath = table.Column<string>(type: "text", nullable: true),
                    SabitFotograflarPath = table.Column<string>(type: "text", nullable: true),
                    YillikBakimFormuPath = table.Column<string>(type: "text", nullable: true),
                    YgIsletmeBelgesiPath = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<Point>(type: "geometry", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Stations");
        }
    }
}
