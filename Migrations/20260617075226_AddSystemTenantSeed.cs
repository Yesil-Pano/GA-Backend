using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GA.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemTenantSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        INSERT INTO ""Tenants"" 
        (""Id"", ""Name"", ""IsActive"", ""CreatedAt"", ""IsDeleted"") 
        VALUES 
        ('00000000-0000-0000-0000-000000000000', 'GA SYS - Sistem Yönetimi', true, (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'), false);
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Tenants\" WHERE \"Id\" = '00000000-0000-0000-0000-000000000000';");
        }
    }
}
