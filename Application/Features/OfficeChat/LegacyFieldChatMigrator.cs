using GA.Core.Domain.Entities;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GA.Application.Features.OfficeChat
{
    /// <summary>
    /// Eski saha↔operasyon (Conversation) geçmişini destek Super Admin ile 1:1 OfficeDirectConversation'a taşır (A3).
    /// </summary>
    public class LegacyFieldChatMigrator
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LegacyFieldChatMigrator> _logger;

        public LegacyFieldChatMigrator(ApplicationDbContext context, ILogger<LegacyFieldChatMigrator> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task MigrateAsync(CancellationToken ct = default)
        {
            if (await _context.OfficeDirectConversations
                    .IgnoreQueryFilters()
                    .AnyAsync(c => c.MigratedFromConversationId != null && !c.IsDeleted, ct))
            {
                return;
            }

            var supportUserId = await ResolveSupportUserIdAsync(ct);
            if (supportUserId == Guid.Empty)
            {
                _logger.LogWarning("Legacy chat migrasyonu atlandı: Super Admin kullanıcı bulunamadı.");
                return;
            }

            var legacyConversations = await _context.Conversations
                .IgnoreQueryFilters()
                .Where(c => !c.IsDeleted)
                .Include(c => c.Messages.Where(m => !m.IsDeleted))
                .ToListAsync(ct);

            var migratedCount = 0;
            foreach (var legacy in legacyConversations)
            {
                if (legacy.Messages.Count == 0) continue;

                var fieldWorkerId = legacy.FieldWorkerUserId;
                var (one, two) = CanonicalPair(fieldWorkerId, supportUserId);

                var direct = await _context.OfficeDirectConversations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c =>
                        !c.IsDeleted &&
                        c.UserOneId == one &&
                        c.UserTwoId == two, ct);

                if (direct == null)
                {
                    direct = new OfficeDirectConversation
                    {
                        UserOneId = one,
                        UserTwoId = two,
                        TenantId = legacy.TenantId,
                        CustomerId = legacy.CustomerId,
                        MigratedFromConversationId = legacy.Id,
                    };
                    _context.OfficeDirectConversations.Add(direct);
                    await _context.SaveChangesAsync(ct);
                }
                else if (direct.MigratedFromConversationId == null)
                {
                    direct.MigratedFromConversationId = legacy.Id;
                }

                foreach (var msg in legacy.Messages.OrderBy(m => m.SentAt))
                {
                    var exists = await _context.OfficeDirectMessages
                        .IgnoreQueryFilters()
                        .AnyAsync(m =>
                            m.LegacyChatMessageId == msg.Id && !m.IsDeleted, ct);
                    if (exists) continue;

                    _context.OfficeDirectMessages.Add(new OfficeDirectMessage
                    {
                        OfficeDirectConversationId = direct.Id,
                        SenderUserId = msg.SenderUserId,
                        Body = msg.Body,
                        SentAt = msg.SentAt,
                        ClientMessageId = msg.ClientMessageId,
                        TenantId = msg.TenantId,
                        CustomerId = msg.CustomerId,
                        LegacyChatMessageId = msg.Id,
                    });
                }

                direct.LastMessageAt = legacy.LastMessageAt
                    ?? legacy.Messages.Max(m => m.SentAt);
                direct.UpdatedAt = DateTime.UtcNow;
                migratedCount++;
            }

            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync(ct);

            if (migratedCount > 0)
                _logger.LogInformation("Legacy saha chat migrasyonu tamamlandı: {Count} konuşma.", migratedCount);
        }

        private async Task<Guid> ResolveSupportUserIdAsync(CancellationToken ct)
        {
            var saUsers = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => !u.IsDeleted && u.IsActive)
                .ToListAsync(ct);

            var support = saUsers.FirstOrDefault(u =>
                u.TenantId == Guid.Empty ||
                string.Equals(u.Email, "admin@theobuz.com", StringComparison.OrdinalIgnoreCase) ||
                u.UserRoles.Any(ur =>
                    ur.Role != null &&
                    !ur.Role.IsDeleted &&
                    string.Equals(ur.Role.Name, "SuperAdmin", StringComparison.OrdinalIgnoreCase)));

            return support?.Id ?? Guid.Empty;
        }

        private static (Guid UserOneId, Guid UserTwoId) CanonicalPair(Guid a, Guid b) =>
            a.CompareTo(b) < 0 ? (a, b) : (b, a);
    }
}
