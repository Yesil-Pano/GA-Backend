namespace GA.Application.Features.Auth
{
    public interface IUserAccessService
    {
        /// <summary>Web sohbet gelen kutusu (ofis ↔ saha listesi).</summary>
        Task<bool> CanAccessOfficeChatInboxAsync(CancellationToken ct = default);

        /// <summary>Ofis ↔ ofis doğrudan mesajlaşma kanalı.</summary>
        Task<bool> CanAccessOfficeDirectChatAsync(CancellationToken ct = default);

        /// <summary>Mobil tek kanal sohbet — yalnızca saha personeli (legacy kontrol).</summary>
        Task<bool> IsFieldWorkerOnlyForChatAsync(CancellationToken ct = default);

        /// <summary>Mobil uygulamada Operasyon sohbet kanalını kullanabilir (tüm aktif personel).</summary>
        Task<bool> CanUseMobileOperationsChatAsync(CancellationToken ct = default);

        Task<bool> IsSuperAdminAsync(CancellationToken ct = default);

        /// <summary>SuperAdmin veya TenantAdmin (firma admin).</summary>
        Task<bool> IsTenantAdminOrAboveAsync(CancellationToken ct = default);

        Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken ct = default);

        Task<bool> IsOperationReporterOnlyAsync(CancellationToken ct = default);

        Task<bool> IsIsgInspectorAsync(CancellationToken ct = default);

        Task<bool> CanViewIsgPhotosAsync(CancellationToken ct = default);

        Task<bool> CanViewOperationPhotosAsync(CancellationToken ct = default);
    }
}
