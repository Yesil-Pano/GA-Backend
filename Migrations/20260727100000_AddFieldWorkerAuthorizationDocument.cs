using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldWorkerAuthorizationDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorizationDocumentFileName",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthorizationDocumentContentType",
                table: "FieldWorkerProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "AuthorizationDocumentData",
                table: "FieldWorkerProfiles",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AuthorizationDocumentFileSize",
                table: "FieldWorkerProfiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AuthorizationDocumentUploadedAt",
                table: "FieldWorkerProfiles",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentFileName",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentContentType",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentData",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentFileSize",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentUploadedAt",
                table: "FieldWorkerProfiles");
        }
    }
}
