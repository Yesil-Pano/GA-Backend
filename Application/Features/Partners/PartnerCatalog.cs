namespace GA.Application.Features.Partners
{
    public record PartnerMetadata(string Key, string Name, string[] Tokens);

    public record PartnerDefinition(string Key, string Name, Guid? TenantId, string[] Tokens);

    public static class PartnerCatalog
    {
        public const string AllKey = "all";

        public static readonly PartnerMetadata Trugo = new(
            "trugo",
            "Trugo Şarj İstasyonları",
            ["trugo"]);

        public static readonly PartnerMetadata Tesla = new(
            "tesla",
            "TESLA",
            ["tesla", "unilever", "algida"]);

        public static readonly PartnerMetadata Astor = new(
            "astor",
            "Astor Enerji",
            ["astor"]);

        public static readonly PartnerMetadata YesilPano = new(
            "yesilpano",
            "Yeşil Pano Projesi",
            ["yeşil", "yesil"]);

        public static IReadOnlyList<PartnerMetadata> All { get; } =
            [Trugo, Tesla, Astor, YesilPano];

        /// <summary>Migration seed ile uyumlu sabit tenant kimlikleri.</summary>
        public static class SeedTenantIds
        {
            public static readonly Guid Trugo = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");
            public static readonly Guid YesilPano = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
            public static readonly Guid Tesla = Guid.Parse("d4e5f6a7-b8c9-4012-d345-678901234501");
            public static readonly Guid Astor = Guid.Parse("d4e5f6a7-b8c9-4012-d345-678901234502");
        }

        public static bool IsAll(string? partnerKey) =>
            string.Equals(partnerKey, AllKey, StringComparison.OrdinalIgnoreCase);

        public static PartnerMetadata? FindMetadata(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (key.Trim().Equals("unilever", StringComparison.OrdinalIgnoreCase))
                return Tesla;
            return All.FirstOrDefault(p =>
                p.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static PartnerDefinition? ResolveFilter(
            string? partnerKey,
            IReadOnlyDictionary<string, Guid> tenantByPartnerKey)
        {
            if (IsAll(partnerKey) || string.IsNullOrWhiteSpace(partnerKey))
                return null;

            var meta = FindMetadata(partnerKey) ?? Trugo;
            tenantByPartnerKey.TryGetValue(meta.Key, out var tenantId);
            return ToDefinition(meta, tenantId == Guid.Empty ? null : tenantId);
        }

        public static PartnerDefinition ToDefinition(PartnerMetadata meta, Guid? tenantId) =>
            new(meta.Key, meta.Name, tenantId, meta.Tokens);

        public static bool Matches(
            PartnerDefinition partner,
            Guid? tenantId,
            string? ownerCompany,
            string? name)
        {
            var hay = $"{ownerCompany} {name}".ToLowerInvariant();

            PartnerMetadata? ownershipHit = null;
            foreach (var p in All)
            {
                if (p.Tokens.Any(t => hay.Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    ownershipHit = p;
                    break;
                }
            }

            if (ownershipHit != null)
                return ownershipHit.Key == partner.Key;

            if (partner.TenantId.HasValue && tenantId.HasValue && partner.TenantId == tenantId)
                return true;

            return false;
        }

        public static bool MatchesTeam(
            PartnerDefinition partner,
            Guid? userTenantId,
            IEnumerable<string>? projectNames)
        {
            var names = (projectNames ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();

            if (names.Count > 0)
                return names.Any(n => Matches(partner, null, null, n));

            return partner.TenantId.HasValue
                   && userTenantId.HasValue
                   && partner.TenantId == userTenantId;
        }
    }
}
