namespace GA.Application.Features.Auth
{
    public interface IUserAccessService
    {
        /// <summary>Web sohbet gelen kutusu (ofis ↔ saha listesi).</summary>
        Task<bool> CanAccessOfficeChatInboxAsync(CancellationToken ct = default);

        /// <summary>Mobil tek kanal sohbet — yalnızca saha personeli.</summary>
        Task<bool> IsFieldWorkerOnlyForChatAsync(CancellationToken ct = default);

        Task<bool> IsSuperAdminAsync(CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);
    }
}
