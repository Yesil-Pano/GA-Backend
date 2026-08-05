using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GA.Application.Features.Partners
{
    public class PartnerTenantService : IPartnerTenantService
    {
        private const string TenantRowsCacheKey = "partner-tenant-rows-v2";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public PartnerTenantService(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IReadOnlyDictionary<string, Guid>> GetTenantMapAsync(CancellationToken ct = default)
        {
            var rows = await GetTenantRowsAsync(ct);
            return rows.ToDictionary(r => r.PartnerKey, r => r.Id, StringComparer.OrdinalIgnoreCase);
        }

        public async Task InvalidateCacheAsync(CancellationToken ct = default)
        {
            _cache.Remove(TenantRowsCacheKey);
            await Task.CompletedTask;
        }

        public async Task<PartnerDefinition?> ResolveFilterAsync(string? partnerKey, CancellationToken ct = default)
        {
            if (PartnerCatalog.IsAll(partnerKey) || string.IsNullOrWhiteSpace(partnerKey))
                return null;

            var normalized = partnerKey.Trim();
            if (normalized.Equals("unilever", StringComparison.OrdinalIgnoreCase))
                normalized = "tesla";

            var rows = await GetTenantRowsAsync(ct);
            var row = rows.FirstOrDefault(r =>
                r.PartnerKey.Equals(normalized, StringComparison.OrdinalIgnoreCase));

            if (row == null)
                return null;

            return ToDefinition(row);
        }

        public async Task<Guid?> GetTenantIdForPartnerKeyAsync(string? partnerKey, CancellationToken ct = default)
        {
            var partner = await ResolveFilterAsync(partnerKey, ct);
            return partner?.TenantId;
        }

        public async Task<IReadOnlyList<PartnerListItemDto>> ListForUiAsync(CancellationToken ct = default)
        {
            var rows = await GetTenantRowsAsync(ct);
            var list = new List<PartnerListItemDto>
            {
                new(PartnerCatalog.AllKey, "TÜMÜ", "*", null, []),
            };

            foreach (var row in rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            {
                var definition = ToDefinition(row);
                list.Add(new PartnerListItemDto(
                    row.PartnerKey,
                    row.Name,
                    GetLetter(row.Name),
                    row.Id,
                    definition.Tokens));
            }

            return list;
        }

        private async Task<IReadOnlyList<PartnerTenantRow>> GetTenantRowsAsync(CancellationToken ct)
        {
            var result = await _cache.GetOrCreateAsync(TenantRowsCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await LoadTenantRowsAsync(ct);
            });
            return result ?? Array.Empty<PartnerTenantRow>();
        }

        private async Task<IReadOnlyList<PartnerTenantRow>> LoadTenantRowsAsync(CancellationToken ct)
        {
            return await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t =>
                    !t.IsDeleted
                    && t.IsActive
                    && t.Id != TenantConstants.SystemTenantId
                    && t.PartnerKey != null
                    && t.PartnerKey != "")
                .OrderBy(t => t.Name)
                .Select(t => new PartnerTenantRow(t.PartnerKey!, t.Id, t.Name))
                .ToListAsync(ct);
        }

        private static PartnerDefinition ToDefinition(PartnerTenantRow row)
        {
            var meta = PartnerCatalog.FindMetadata(row.PartnerKey);
            var tokens = meta?.Tokens ?? BuildDefaultTokens(row.PartnerKey, row.Name);
            var displayName = meta?.Name ?? row.Name;
            return new PartnerDefinition(row.PartnerKey, displayName, row.Id, tokens);
        }

        internal static string[] BuildDefaultTokens(string partnerKey, string name)
        {
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                partnerKey.Trim().ToLowerInvariant(),
            };

            foreach (var part in name.Split([' ', '-', '/', '.'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length >= 3)
                    tokens.Add(part.ToLowerInvariant());
            }

            return tokens.ToArray();
        }

        internal static string GetLetter(string name)
        {
            var trimmed = name.Trim();
            return trimmed.Length > 0 ? trimmed[..1].ToUpperInvariant() : "?";
        }

        private sealed record PartnerTenantRow(string PartnerKey, Guid Id, string Name);
    }
}
