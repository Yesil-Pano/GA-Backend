using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWorkOrdersArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                table: "WorkOrders",
                newName: "WorkType");

            migrationBuilder.RenameColumn(
                name: "PlannedDate",
                table: "WorkOrders",
                newName: "StartDate");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "WorkOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "MobileDescription",
                table: "WorkOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OpenedByUserId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OperationUserId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkCategory",
                table: "WorkOrders",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "MobileDescription",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "OpenedByUserId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "OperationUserId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "WorkCategory",
                table: "WorkOrders");

            migrationBuilder.RenameColumn(
                name: "WorkType",
                table: "WorkOrders",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "WorkOrders",
                newName: "PlannedDate");
        }
    }
}
