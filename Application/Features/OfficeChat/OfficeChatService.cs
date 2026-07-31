using GA.Application.Features.Auth;
using GA.Application.Features.OfficeChat.DTOs;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.OfficeChat
{
    public interface IOfficeChatService
    {
        Task<List<OfficeDirectConversationDto>> ListConversationsAsync(CancellationToken ct = default);
        Task<List<OfficeDirectMessageDto>> GetMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct = default);
        Task<OfficeDirectConversationDto> StartConversationAsync(
            Guid targetUserId, CancellationToken ct = default);
        Task<OfficeDirectMessageDto> SendMessageAsync(
            Guid conversationId, SendOfficeMessageRequest request, CancellationToken ct = default);
        Task MarkReadAsync(Guid conversationId, CancellationToken ct = default);
    }

    public class OfficeChatService : IOfficeChatService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserAccessService _userAccess;

        public OfficeChatService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IUserAccessService userAccess)
        {
            _context = context;
            _currentUser = currentUser;
            _userAccess = userAccess;
        }

        public async Task<List<OfficeDirectConversationDto>> ListConversationsAsync(CancellationToken ct = default)
        {
            await EnsureOfficeAccessAsync(ct);

            var me = _currentUser.UserId;
            var isSuperAdmin = await _userAccess.IsSuperAdminAsync(ct);
            var tenantId = _currentUser.TenantId;

            var officeUsers = await LoadEligibleOfficeUsersAsync(tenantId, isSuperAdmin, ct);
            officeUsers = officeUsers.Where(u => u.Id != me).ToList();

            var userIds = officeUsers.Select(u => u.Id).ToList();
            var existing = await _context.OfficeDirectConversations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c =>
                    !c.IsDeleted &&
                    (c.UserOneId == me || c.UserTwoId == me) &&
                    (userIds.Contains(c.UserOneId) || userIds.Contains(c.UserTwoId)))
                .ToListAsync(ct);

            var convByOther = new Dictionary<Guid, OfficeDirectConversation>();
            foreach (var conv in existing)
            {
                var otherId = conv.UserOneId == me ? conv.UserTwoId : conv.UserOneId;
                convByOther[otherId] = conv;
            }

            var convIds = existing.Select(c => c.Id).ToList();
            var lastByConv = new Dictionary<Guid, (string? Body, DateTime SentAt)>();
            if (convIds.Count > 0)
            {
                var lastMessages = await _context.OfficeDirectMessages
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(m => convIds.Contains(m.OfficeDirectConversationId) && !m.IsDeleted)
                    .GroupBy(m => m.OfficeDirectConversationId)
                    .Select(g => new
                    {
                        ConversationId = g.Key,
                        Body = g.OrderByDescending(x => x.SentAt).Select(x => x.Body).FirstOrDefault(),
                        SentAt = g.Max(x => x.SentAt),
                    })
                    .ToListAsync(ct);

                foreach (var lm in lastMessages)
                    lastByConv[lm.ConversationId] = (lm.Body, lm.SentAt);
            }

            var result = new List<OfficeDirectConversationDto>();
            foreach (var user in officeUsers)
            {
                convByOther.TryGetValue(user.Id, out var conv);
                lastByConv.TryGetValue(conv?.Id ?? Guid.Empty, out var last);

                result.Add(new OfficeDirectConversationDto
                {
                    Id = conv?.Id,
                    OtherUserId = user.Id,
                    OtherUserName = user.FullName,
                    LastMessageAt = conv != null && lastByConv.ContainsKey(conv.Id)
                        ? last.SentAt
                        : conv?.LastMessageAt,
                    LastMessagePreview = Truncate(conv != null && lastByConv.ContainsKey(conv.Id) ? last.Body : null, 80),
                    UnreadCount = conv == null
                        ? 0
                        : await CountUnreadAsync(conv.Id, me, ct),
                });
            }

            return result
                .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(c => c.OtherUserName)
                .ToList();
        }

        public async Task<List<OfficeDirectMessageDto>> GetMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            await EnsureOfficeAccessAsync(ct);
            await EnsureParticipantAsync(conversationId, ct);
            return await LoadMessagesAsync(conversationId, before, take, ct);
        }

        public async Task<OfficeDirectConversationDto> StartConversationAsync(
            Guid targetUserId, CancellationToken ct = default)
        {
            await EnsureOfficeAccessAsync(ct);

            if (targetUserId == _currentUser.UserId)
                throw new InvalidOperationException("Kendinizle konuşma başlatamazsınız.");

            if (!await IsUserEligibleForOfficeDirectChatAsync(targetUserId, ct))
                throw new InvalidOperationException("Bu kullanıcıyla ofis mesajlaşması yapılamaz.");

            await EnsureSameTenantAsync(targetUserId, ct);

            var conv = await GetOrCreateConversationAsync(_currentUser.UserId, targetUserId, ct);
            var other = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var unread = await CountUnreadAsync(conv.Id, _currentUser.UserId, ct);
            var last = await _context.OfficeDirectMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.OfficeDirectConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Body, m.SentAt })
                .FirstOrDefaultAsync(ct);

            return new OfficeDirectConversationDto
            {
                Id = conv.Id,
                OtherUserId = other.Id,
                OtherUserName = other.FullName,
                LastMessageAt = last?.SentAt ?? conv.LastMessageAt,
                LastMessagePreview = Truncate(last?.Body, 80),
                UnreadCount = unread,
            };
        }

        public async Task<OfficeDirectMessageDto> SendMessageAsync(
            Guid conversationId, SendOfficeMessageRequest request, CancellationToken ct = default)
        {
            await EnsureOfficeAccessAsync(ct);
            await EnsureParticipantAsync(conversationId, ct);

            var body = (request.Body ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(body))
                throw new InvalidOperationException("Mesaj boş olamaz.");
            if (body.Length > 2000)
                throw new InvalidOperationException("Mesaj en fazla 2000 karakter olabilir.");

            var conv = await GetConversationEntityAsync(conversationId, ct)
                ?? throw new KeyNotFoundException("Konuşma bulunamadı.");

            if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
            {
                var existing = await _context.OfficeDirectMessages
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.OfficeDirectConversationId == conv.Id &&
                        m.SenderUserId == _currentUser.UserId &&
                        m.ClientMessageId == request.ClientMessageId &&
                        !m.IsDeleted, ct);

                if (existing != null)
                    return await MapMessageAsync(existing, ct);
            }

            var msg = new OfficeDirectMessage
            {
                OfficeDirectConversationId = conv.Id,
                SenderUserId = _currentUser.UserId,
                Body = body,
                SentAt = DateTime.UtcNow,
                ClientMessageId = string.IsNullOrWhiteSpace(request.ClientMessageId)
                    ? null
                    : request.ClientMessageId.Trim(),
                TenantId = conv.TenantId,
                CustomerId = conv.CustomerId,
            };

            _context.OfficeDirectMessages.Add(msg);
            conv.LastMessageAt = msg.SentAt;
            conv.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            await UpsertReadStateAsync(conv.Id, _currentUser.UserId, msg.SentAt, ct);

            return await MapMessageAsync(msg, ct);
        }

        public async Task MarkReadAsync(Guid conversationId, CancellationToken ct = default)
        {
            await EnsureOfficeAccessAsync(ct);
            await EnsureParticipantAsync(conversationId, ct);
            await UpsertReadStateAsync(conversationId, _currentUser.UserId, DateTime.UtcNow, ct);
        }

        private async Task EnsureOfficeAccessAsync(CancellationToken ct)
        {
            if (_currentUser.UserId == Guid.Empty)
                throw new UnauthorizedAccessException("Oturum gerekli.");

            if (!await _userAccess.CanAccessOfficeDirectChatAsync(ct))
                throw new UnauthorizedAccessException("Ofis mesajlaşmasına erişim yetkiniz yok.");
        }

        private async Task<List<(Guid Id, string FullName)>> LoadEligibleOfficeUsersAsync(
            Guid tenantId, bool isSuperAdmin, CancellationToken ct)
        {
            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Where(u => !u.IsDeleted && (isSuperAdmin || tenantId == Guid.Empty || u.TenantId == tenantId))
                .ToListAsync(ct);

            var result = new List<(Guid Id, string FullName)>();
            foreach (var user in users)
            {
                if (await IsUserEligibleForOfficeDirectChatAsync(user, ct))
                    result.Add((user.Id, user.FullName));
            }

            return result;
        }

        private async Task<bool> IsUserEligibleForOfficeDirectChatAsync(Guid userId, CancellationToken ct)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

            return user != null && await IsUserEligibleForOfficeDirectChatAsync(user, ct);
        }

        private Task<bool> IsUserEligibleForOfficeDirectChatAsync(User user, CancellationToken ct)
        {
            if (string.Equals(user.Email, "admin@theobuz.com", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(true);

            var roles = user.UserRoles
                .Where(ur => ur.Role != null && !ur.Role.IsDeleted)
                .Select(ur => ur.Role!.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(roles.Any(r => RoleNames.OfficeDirectChatRoles.Contains(r)));
        }

        private async Task EnsureSameTenantAsync(Guid targetUserId, CancellationToken ct)
        {
            if (await _userAccess.IsSuperAdminAsync(ct))
                return;

            var tenantId = _currentUser.TenantId;
            if (tenantId == Guid.Empty) return;

            var sameTenant = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(u => u.Id == targetUserId && !u.IsDeleted && u.TenantId == tenantId, ct);

            if (!sameTenant)
                throw new UnauthorizedAccessException("Bu kullanıcıyla mesajlaşamazsınız.");
        }

        private static (Guid UserOneId, Guid UserTwoId) CanonicalPair(Guid a, Guid b) =>
            a.CompareTo(b) < 0 ? (a, b) : (b, a);

        private async Task<OfficeDirectConversation> GetOrCreateConversationAsync(
            Guid userA, Guid userB, CancellationToken ct)
        {
            var (one, two) = CanonicalPair(userA, userB);
            var isSuperAdmin = await _userAccess.IsSuperAdminAsync(ct);
            var tenantId = _currentUser.TenantId;

            var existing = await _context.OfficeDirectConversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c =>
                    !c.IsDeleted &&
                    c.UserOneId == one &&
                    c.UserTwoId == two &&
                    (isSuperAdmin || tenantId == Guid.Empty || c.TenantId == tenantId), ct);

            if (existing != null) return existing;

            var otherId = userA == one ? two : one;
            var other = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == otherId && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var me = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var convTenantId = tenantId != Guid.Empty ? tenantId : other.TenantId;
            var conv = new OfficeDirectConversation
            {
                UserOneId = one,
                UserTwoId = two,
                TenantId = convTenantId,
                CustomerId = me.CustomerId ?? other.CustomerId,
            };

            _context.OfficeDirectConversations.Add(conv);
            await _context.SaveChangesAsync(ct);
            return conv;
        }

        private async Task<OfficeDirectConversation?> GetConversationEntityAsync(Guid id, CancellationToken ct) =>
            await _context.OfficeDirectConversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        private async Task EnsureParticipantAsync(Guid conversationId, CancellationToken ct)
        {
            var conv = await GetConversationEntityAsync(conversationId, ct)
                ?? throw new KeyNotFoundException("Konuşma bulunamadı.");

            var me = _currentUser.UserId;
            if (conv.UserOneId != me && conv.UserTwoId != me)
                throw new UnauthorizedAccessException("Bu konuşmaya erişemezsiniz.");

            if (!await _userAccess.IsSuperAdminAsync(ct))
            {
                var tenantId = _currentUser.TenantId;
                if (tenantId != Guid.Empty && conv.TenantId != tenantId)
                    throw new UnauthorizedAccessException("Bu konuşmaya erişemezsiniz.");
            }
        }

        private async Task<int> CountUnreadAsync(Guid conversationId, Guid userId, CancellationToken ct)
        {
            var lastRead = await _context.OfficeDirectReadStates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r =>
                    r.OfficeDirectConversationId == conversationId &&
                    r.UserId == userId &&
                    !r.IsDeleted)
                .Select(r => (DateTime?)r.LastReadAt)
                .FirstOrDefaultAsync(ct);

            var cutoff = lastRead ?? DateTime.MinValue;

            return await _context.OfficeDirectMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(m =>
                    m.OfficeDirectConversationId == conversationId &&
                    !m.IsDeleted &&
                    m.SenderUserId != userId &&
                    m.SentAt > cutoff, ct);
        }

        private async Task UpsertReadStateAsync(
            Guid conversationId, Guid userId, DateTime lastReadAt, CancellationToken ct)
        {
            var state = await _context.OfficeDirectReadStates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r =>
                    r.OfficeDirectConversationId == conversationId &&
                    r.UserId == userId &&
                    !r.IsDeleted, ct);

            if (state == null)
            {
                var conv = await GetConversationEntityAsync(conversationId, ct);
                state = new OfficeDirectReadState
                {
                    OfficeDirectConversationId = conversationId,
                    UserId = userId,
                    LastReadAt = lastReadAt,
                    TenantId = conv?.TenantId ?? _currentUser.TenantId,
                    CustomerId = conv?.CustomerId ?? _currentUser.CustomerId,
                };
                _context.OfficeDirectReadStates.Add(state);
            }
            else if (lastReadAt > state.LastReadAt)
            {
                state.LastReadAt = lastReadAt;
                state.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task<List<OfficeDirectMessageDto>> LoadMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct)
        {
            var q = _context.OfficeDirectMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.OfficeDirectConversationId == conversationId && !m.IsDeleted);

            if (before.HasValue)
                q = q.Where(m => m.SentAt < before.Value);

            var rows = await q
                .OrderByDescending(m => m.SentAt)
                .Take(take)
                .ToListAsync(ct);

            rows.Reverse();

            var senderIds = rows.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            var me = _currentUser.UserId;
            return rows.Select(m => new OfficeDirectMessageDto
            {
                Id = m.Id,
                ConversationId = m.OfficeDirectConversationId,
                SenderUserId = m.SenderUserId,
                SenderName = senders.GetValueOrDefault(m.SenderUserId, "Kullanıcı"),
                IsMine = m.SenderUserId == me,
                Body = m.Body,
                SentAt = m.SentAt,
                ClientMessageId = m.ClientMessageId,
            }).ToList();
        }

        private async Task<OfficeDirectMessageDto> MapMessageAsync(OfficeDirectMessage m, CancellationToken ct)
        {
            var name = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == m.SenderUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct) ?? "Kullanıcı";

            return new OfficeDirectMessageDto
            {
                Id = m.Id,
                ConversationId = m.OfficeDirectConversationId,
                SenderUserId = m.SenderUserId,
                SenderName = name,
                IsMine = m.SenderUserId == _currentUser.UserId,
                Body = m.Body,
                SentAt = m.SentAt,
                ClientMessageId = m.ClientMessageId,
            };
        }

        private static string? Truncate(string? text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= max ? text : text[..max] + "…";
        }
    }
}
