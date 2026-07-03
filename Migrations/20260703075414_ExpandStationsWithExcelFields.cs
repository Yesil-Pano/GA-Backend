using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class ExpandStationsWithExcelFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargepointId",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevicePower",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceVendor",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedDate",
                table: "Stations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerCompany",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerStatus",
                table: "Stations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SocketCount",
                table: "Stations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorModel",
                table: "Stations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargepointId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "DevicePower",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "DeviceVendor",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "EstimatedDate",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "OwnerCompany",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "PartnerStatus",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "SocketCount",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "VendorModel",
                table: "Stations");
        }
    }
}
