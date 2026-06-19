using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class SecureMultiTenantTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "FieldWorkerProfiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamLeader",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "FieldWorkerProfiles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "TeamLeader",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FieldWorkerProfiles");
        }
    }
}
