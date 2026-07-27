using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldWorkerDocumentsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldWorkerDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldWorkerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Data = table.Column<byte[]>(type: "bytea", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldWorkerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldWorkerDocuments_FieldWorkerProfiles_FieldWorkerProfileId",
                        column: x => x.FieldWorkerProfileId,
                        principalTable: "FieldWorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldWorkerDocuments_FieldWorkerProfileId_DocumentType",
                table: "FieldWorkerDocuments",
                columns: new[] { "FieldWorkerProfileId", "DocumentType" });

            // Mevcut Yetki Belgesi kolonlarından migrate
            migrationBuilder.Sql("""
                INSERT INTO "FieldWorkerDocuments" (
                    "Id", "FieldWorkerProfileId", "DocumentType", "FileName", "ContentType",
                    "Data", "FileSize", "UploadedAt", "TenantId", "CustomerId",
                    "CreatedAt", "UpdatedAt", "IsDeleted"
                )
                SELECT
                    gen_random_uuid(),
                    p."Id",
                    'Authorization',
                    COALESCE(NULLIF(p."AuthorizationDocumentFileName", ''), 'yetki-belgesi.pdf'),
                    COALESCE(NULLIF(p."AuthorizationDocumentContentType", ''), 'application/pdf'),
                    p."AuthorizationDocumentData",
                    COALESCE(p."AuthorizationDocumentFileSize", octet_length(p."AuthorizationDocumentData")),
                    COALESCE(p."AuthorizationDocumentUploadedAt", NOW() AT TIME ZONE 'utc'),
                    p."TenantId",
                    p."CustomerId",
                    NOW() AT TIME ZONE 'utc',
                    NULL,
                    FALSE
                FROM "FieldWorkerProfiles" p
                WHERE p."AuthorizationDocumentData" IS NOT NULL
                  AND octet_length(p."AuthorizationDocumentData") > 0;
                """);

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentContentType",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentData",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentFileName",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentFileSize",
                table: "FieldWorkerProfiles");

            migrationBuilder.DropColumn(
                name: "AuthorizationDocumentUploadedAt",
                table: "FieldWorkerProfiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.AddColumn<string>(
                name: "AuthorizationDocumentFileName",
                table: "FieldWorkerProfiles",
                type: "text",
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

            migrationBuilder.Sql("""
                UPDATE "FieldWorkerProfiles" p
                SET
                    "AuthorizationDocumentFileName" = d."FileName",
                    "AuthorizationDocumentContentType" = d."ContentType",
                    "AuthorizationDocumentData" = d."Data",
                    "AuthorizationDocumentFileSize" = d."FileSize",
                    "AuthorizationDocumentUploadedAt" = d."UploadedAt"
                FROM (
                    SELECT DISTINCT ON ("FieldWorkerProfileId") *
                    FROM "FieldWorkerDocuments"
                    WHERE "DocumentType" = 'Authorization' AND "IsDeleted" = FALSE
                    ORDER BY "FieldWorkerProfileId", "UploadedAt" DESC
                ) d
                WHERE p."Id" = d."FieldWorkerProfileId";
                """);

            migrationBuilder.DropTable(name: "FieldWorkerDocuments");
        }
    }
}
