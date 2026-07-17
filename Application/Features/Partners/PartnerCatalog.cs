namespace GA.Application.Features.Partners
{
    public record PartnerDefinition(string Key, string Name, Guid? TenantId, string[] Tokens);

    public static class PartnerCatalog
    {
        public const string AllKey = "all";

        public static readonly PartnerDefinition Trugo = new(
            "trugo",
            "Trugo Şarj İstasyonları",
            Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce"),
            ["trugo"]);

        public static readonly PartnerDefinition Tesla = new(
            "tesla",
            "TESLA",
            null,
            // Eski Unilever Algida verisi + yeni TESLA adı
            ["tesla", "unilever", "algida"]);

        public static readonly PartnerDefinition Astor = new(
            "astor",
            "Astor Enerji",
            null,
            ["astor"]);

        public static readonly PartnerDefinition YesilPano = new(
            "yesilpano",
            "Yeşil Pano Projesi",
            Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0"),
            ["yeşil", "yesil"]);

        public static IReadOnlyList<PartnerDefinition> All { get; } =
            [Trugo, Tesla, Astor, YesilPano];

        public static bool IsAll(string? partnerKey) =>
            string.Equals(partnerKey, AllKey, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Super Admin filtre anahtarı. "all" veya boş → filtre yok (null).
        /// Bilinmeyen key → Trugo (geriye dönük varsayılan).
        /// Eski "unilever" key → TESLA.
        /// </summary>
        public static PartnerDefinition? ResolveFilter(string? partnerKey)
        {
            if (IsAll(partnerKey) || string.IsNullOrWhiteSpace(partnerKey))
                return null;

            var key = partnerKey.Trim();
            if (key.Equals("unilever", StringComparison.OrdinalIgnoreCase))
                return Tesla;

            return Find(key) ?? Trugo;
        }

        public static PartnerDefinition? Find(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (key.Trim().Equals("unilever", StringComparison.OrdinalIgnoreCase))
                return Tesla;
            return All.FirstOrDefault(p =>
                p.Key.Equals(key.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// OwnerCompany / isim token'ı (TRUGO vb.) TenantId'den önce gelir.
        /// Böylece yanlış TenantId yazılmış Trugo noktaları Yeşil Pano'da görünmez.
        /// </summary>
        public static bool Matches(
            PartnerDefinition partner,
            Guid? tenantId,
            string? ownerCompany,
            string? name)
        {
            var hay = $"{ownerCompany} {name}".ToLowerInvariant();

            PartnerDefinition? ownershipHit = null;
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

        /// <summary>
        /// Ekip: proje adlarından herhangi biri firmaya uyuyorsa dahil et.
        /// Proje yoksa kullanıcı TenantId'sine bak.
        /// </summary>
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
