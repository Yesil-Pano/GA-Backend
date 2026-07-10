using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressToFieldWorkerProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "City",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "District",
                table: "FieldWorkerProfiles");
        }
    }
}
