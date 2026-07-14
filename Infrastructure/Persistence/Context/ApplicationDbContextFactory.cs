using GA.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GA.Infrastructure.Persistence.Context
{
    /// <summary>
    /// EF Tools (dotnet ef migrations) için design-time factory.
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();
            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseNetTopologySuite());

            return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService());
        }

        private sealed class DesignTimeCurrentUserService : ICurrentUserService
        {
            public Guid UserId => Guid.Empty;
            public Guid TenantId => Guid.Empty;
            public Guid? CustomerId => null;
        }
    }
}
