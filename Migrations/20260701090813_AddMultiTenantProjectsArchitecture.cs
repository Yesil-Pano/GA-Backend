using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenantProjectsArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldWorkerProfileProjects",
                columns: table => new
                {
                    FieldWorkerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldWorkerProfileProjects", x => new { x.FieldWorkerProfileId, x.ProjectId });
                    table.ForeignKey(
                        name: "FK_FieldWorkerProfileProjects_FieldWorkerProfiles_FieldWorkerP~",
                        column: x => x.FieldWorkerProfileId,
                        principalTable: "FieldWorkerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FieldWorkerProfileProjects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldWorkerProfileProjects_ProjectId",
                table: "FieldWorkerProfileProjects",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldWorkerProfileProjects");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
