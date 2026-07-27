using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Snapshot senkronu: TargetUserId ve UserPushTokens zaten DB'de mevcut.
    /// Şema değişikliği yok.
    /// </remarks>
    public partial class SyncModelResidual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
