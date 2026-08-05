namespace GA.Application.Features.Partners
{
    public record PartnerListItemDto(
        string Key,
        string Name,
        string Letter,
        Guid? TenantId,
        string[] Tokens);

    public interface IPartnerTenantService
    {
        Task<IReadOnlyDictionary<string, Guid>> GetTenantMapAsync(CancellationToken ct = default);

        Task<PartnerDefinition?> ResolveFilterAsync(string? partnerKey, CancellationToken ct = default);

        Task<Guid?> GetTenantIdForPartnerKeyAsync(string? partnerKey, CancellationToken ct = default);

        Task<IReadOnlyList<PartnerListItemDto>> ListForUiAsync(CancellationToken ct = default);

        Task InvalidateCacheAsync(CancellationToken ct = default);
    }
}
