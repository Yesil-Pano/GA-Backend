using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderStationCityDistrictIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DistrictId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "Stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DistrictId",
                table: "Stations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_CityId",
                table: "WorkOrders",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_DistrictId",
                table: "WorkOrders",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_CityId",
                table: "Stations",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_Stations_DistrictId",
                table: "Stations",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_Cities_CityId",
                table: "Stations",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Stations_Districts_DistrictId",
                table: "Stations",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Cities_CityId",
                table: "WorkOrders",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Districts_DistrictId",
                table: "WorkOrders",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stations_Cities_CityId",
                table: "Stations");

            migrationBuilder.DropForeignKey(
                name: "FK_Stations_Districts_DistrictId",
                table: "Stations");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Cities_CityId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Districts_DistrictId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_CityId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_DistrictId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Stations_CityId",
                table: "Stations");

            migrationBuilder.DropIndex(
                name: "IX_Stations_DistrictId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "Stations");
        }
    }
}
