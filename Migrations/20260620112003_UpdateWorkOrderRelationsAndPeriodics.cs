using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class UpdateWorkOrderRelationsAndPeriodics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPeriodic",
                table: "WorkOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextExecutionDate",
                table: "WorkOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceInterval",
                table: "WorkOrders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedToUserId",
                table: "WorkOrders",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_OpenedByUserId",
                table: "WorkOrders",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_OperationUserId",
                table: "WorkOrders",
                column: "OperationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_AssignedToUserId",
                table: "WorkOrders",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_OpenedByUserId",
                table: "WorkOrders",
                column: "OpenedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_Users_OperationUserId",
                table: "WorkOrders",
                column: "OperationUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_AssignedToUserId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_OpenedByUserId",
                table: "WorkOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_Users_OperationUserId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_AssignedToUserId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_OpenedByUserId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_OperationUserId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsPeriodic",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "NextExecutionDate",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "RecurrenceInterval",
                table: "WorkOrders");
        }
    }
}
