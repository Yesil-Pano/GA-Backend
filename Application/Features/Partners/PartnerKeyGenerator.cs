using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.Partners
{
    public static partial class PartnerKeyGenerator
    {
        public static string? Slugify(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var normalized = value.Trim().ToLowerInvariant()
                .Replace('ı', 'i')
                .Replace('ğ', 'g')
                .Replace('ü', 'u')
                .Replace('ş', 's')
                .Replace('ö', 'o')
                .Replace('ç', 'c');

            normalized = NonAlphaNumeric().Replace(normalized, string.Empty);
            return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        public static async Task<string?> ResolveUniquePartnerKeyAsync(
            ApplicationDbContext context,
            string name,
            string? requestedKey = null,
            CancellationToken ct = default)
        {
            var baseKey = Slugify(requestedKey) ?? Slugify(name);
            if (baseKey == null) return null;

            var candidate = baseKey;
            var suffix = 2;
            while (await context.Tenants
                       .IgnoreQueryFilters()
                       .AnyAsync(
                           t => !t.IsDeleted
                                && t.PartnerKey != null
                                && t.PartnerKey.ToLower() == candidate.ToLower(),
                           ct))
            {
                candidate = $"{baseKey}{suffix}";
                suffix++;
            }

            return candidate;
        }

        [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
        private static partial Regex NonAlphaNumeric();
    }
}
