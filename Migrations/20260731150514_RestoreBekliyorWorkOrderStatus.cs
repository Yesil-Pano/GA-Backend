using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class RestoreBekliyorWorkOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE public."WorkOrders"
                SET "Status" = 'Bekliyor'
                WHERE "IsDeleted" = FALSE
                  AND "Status" = 'Devam Ediyor'
                  AND "AssignedToUserId" IS NOT NULL
                  AND "StartedAt" IS NULL
                  AND "CompletedAt" IS NULL
                  AND "CancelledAt" IS NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE public."WorkOrders"
                SET "Status" = 'Atanmamış'
                WHERE "IsDeleted" = FALSE
                  AND "Status" = 'Devam Ediyor'
                  AND "AssignedToUserId" IS NULL
                  AND "CompletedAt" IS NULL
                  AND "CancelledAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE public."WorkOrders"
                SET "Status" = 'Devam Ediyor'
                WHERE "IsDeleted" = FALSE
                  AND "Status" IN ('Bekliyor', 'Atanmamış');
                """);
        }
    }
}
