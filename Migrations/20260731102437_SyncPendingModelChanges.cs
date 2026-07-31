using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentWorkOrderId",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PeriodLabel",
                table: "WorkOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OfficeDirectConversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserOneId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserTwoId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeDirectConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficeDirectMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeDirectConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeDirectMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfficeDirectMessages_OfficeDirectConversations_OfficeDirect~",
                        column: x => x.OfficeDirectConversationId,
                        principalTable: "OfficeDirectConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfficeDirectReadStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficeDirectConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficeDirectReadStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfficeDirectReadStates_OfficeDirectConversations_OfficeDire~",
                        column: x => x.OfficeDirectConversationId,
                        principalTable: "OfficeDirectConversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeDirectConversations_Tenant_UserPair",
                table: "OfficeDirectConversations",
                columns: new[] { "TenantId", "UserOneId", "UserTwoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficeDirectMessages_OfficeDirectConversationId",
                table: "OfficeDirectMessages",
                column: "OfficeDirectConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficeDirectMessages_OfficeDirectConversationId_SenderUserI~",
                table: "OfficeDirectMessages",
                columns: new[] { "OfficeDirectConversationId", "SenderUserId", "ClientMessageId" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficeDirectReadStates_Conversation_User",
                table: "OfficeDirectReadStates",
                columns: new[] { "OfficeDirectConversationId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficeDirectMessages");

            migrationBuilder.DropTable(
                name: "OfficeDirectReadStates");

            migrationBuilder.DropTable(
                name: "OfficeDirectConversations");

            migrationBuilder.DropColumn(
                name: "ParentWorkOrderId",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PeriodLabel",
                table: "WorkOrders");
        }
    }
}
