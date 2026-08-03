using GA.Application.Features.Auth;
using GA.Application.Features.OfficeChat.DTOs;
using GA.Application.Features.Partners;
using GA.Core.Domain.Constants;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.OfficeChat
{
    public interface IOfficeChatService
    {
        Task<List<DirectContactDto>> ListContactsAsync(string? partnerKey, CancellationToken ct = default);
        Task<List<OfficeDirectConversationDto>> ListConversationsAsync(CancellationToken ct = default);
        Task<List<OfficeDirectMessageDto>> GetMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct = default);
        Task<DirectContactDto> StartConversationAsync(
            Guid targetUserId, CancellationToken ct = default);
        Task<(OfficeDirectMessageDto SenderMessage, OfficeDirectMessageDto RecipientMessage, Guid RecipientUserId)> SendMessageAsync(
            Guid conversationId, SendOfficeMessageRequest request, CancellationToken ct = default);
        Task<(Guid ConversationId, Guid UserId, DateTime LastReadAt)> MarkReadAsync(
            Guid conversationId, CancellationToken ct = default);
        Task<int> GetUnreadTotalAsync(CancellationToken ct = default);
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
            var contacts = await ListContactsAsync(partnerKey: null, ct);
            return contacts.Select(MapToLegacyDto).ToList();
        }

        public async Task<List<DirectContactDto>> ListContactsAsync(string? partnerKey, CancellationToken ct = default)
        {
            await EnsureChatAccessAsync(ct);
            var me = _currentUser.UserId;
            var meIsGa = await _userAccess.IsSuperAdminAsync(ct);
            var partnerFilter = meIsGa ? PartnerCatalog.ResolveFilter(partnerKey) : null;

            var contacts = await LoadEligibleContactsAsync(me, meIsGa, partnerFilter, ct);
            var contactIds = contacts.Select(c => c.UserId).ToList();

            var existing = await _context.OfficeDirectConversations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c =>
                    !c.IsDeleted &&
                    (c.UserOneId == me || c.UserTwoId == me) &&
                    (contactIds.Contains(c.UserOneId) || contactIds.Contains(c.UserTwoId)))
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

            var result = new List<DirectContactDto>();
            foreach (var contact in contacts)
            {
                convByOther.TryGetValue(contact.UserId, out var conv);
                lastByConv.TryGetValue(conv?.Id ?? Guid.Empty, out var last);

                result.Add(new DirectContactDto
                {
                    ConversationId = conv?.Id,
                    UserId = contact.UserId,
                    FullName = contact.FullName,
                    IsGaManagement = contact.IsGaManagement,
                    BadgeLabel = contact.BadgeLabel,
                    CompanyName = contact.CompanyName,
                    LastMessageAt = conv != null && lastByConv.ContainsKey(conv.Id)
                        ? last.SentAt
                        : conv?.LastMessageAt,
                    LastMessagePreview = Truncate(
                        conv != null && lastByConv.ContainsKey(conv.Id) ? last.Body : null, 80),
                    UnreadCount = conv == null
                        ? 0
                        : await CountUnreadAsync(conv.Id, me, ct),
                });
            }

            return result
                .OrderByDescending(c => c.IsGaManagement)
                .ThenByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(c => c.FullName, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false))
                .ToList();
        }

        public async Task<List<OfficeDirectMessageDto>> GetMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            await EnsureChatAccessAsync(ct);
            await EnsureParticipantAsync(conversationId, ct);
            return await LoadMessagesAsync(conversationId, before, take, ct);
        }

        public async Task<DirectContactDto> StartConversationAsync(
            Guid targetUserId, CancellationToken ct = default)
        {
            await EnsureChatAccessAsync(ct);

            if (targetUserId == _currentUser.UserId)
                throw new InvalidOperationException("Kendinizle konuşma başlatamazsınız.");

            await EnsureCanMessageAsync(targetUserId, ct);

            var conv = await GetOrCreateConversationAsync(_currentUser.UserId, targetUserId, ct);
            var other = await LoadUserContactAsync(targetUserId, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var unread = await CountUnreadAsync(conv.Id, _currentUser.UserId, ct);
            var last = await _context.OfficeDirectMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.OfficeDirectConversationId == conv.Id && !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Select(m => new { m.Body, m.SentAt })
                .FirstOrDefaultAsync(ct);

            return new DirectContactDto
            {
                ConversationId = conv.Id,
                UserId = other.UserId,
                FullName = other.FullName,
                IsGaManagement = other.IsGaManagement,
                BadgeLabel = other.BadgeLabel,
                CompanyName = other.CompanyName,
                LastMessageAt = last?.SentAt ?? conv.LastMessageAt,
                LastMessagePreview = Truncate(last?.Body, 80),
                UnreadCount = unread,
            };
        }

        public async Task<(OfficeDirectMessageDto SenderMessage, OfficeDirectMessageDto RecipientMessage, Guid RecipientUserId)> SendMessageAsync(
            Guid conversationId, SendOfficeMessageRequest request, CancellationToken ct = default)
        {
            await EnsureChatAccessAsync(ct);
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
                {
                    var recipientExisting = conv.UserOneId == _currentUser.UserId ? conv.UserTwoId : conv.UserOneId;
                    var senderExisting = await MapMessageAsync(existing, conv, _currentUser.UserId, ct);
                    var recipientMappedExisting = await MapMessageAsync(existing, conv, recipientExisting, ct);
                    return (senderExisting, recipientMappedExisting, recipientExisting);
                }
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

            var senderMapped = await MapMessageAsync(msg, conv, _currentUser.UserId, ct);
            var recipientId = conv.UserOneId == _currentUser.UserId ? conv.UserTwoId : conv.UserOneId;
            var recipientMapped = await MapMessageAsync(msg, conv, recipientId, ct);
            return (senderMapped, recipientMapped, recipientId);
        }

        public async Task<(Guid ConversationId, Guid UserId, DateTime LastReadAt)> MarkReadAsync(
            Guid conversationId, CancellationToken ct = default)
        {
            await EnsureChatAccessAsync(ct);
            await EnsureParticipantAsync(conversationId, ct);
            var now = DateTime.UtcNow;
            await UpsertReadStateAsync(conversationId, _currentUser.UserId, now, ct);
            return (conversationId, _currentUser.UserId, now);
        }

        public async Task<int> GetUnreadTotalAsync(CancellationToken ct = default)
        {
            await EnsureChatAccessAsync(ct);
            var contacts = await ListContactsAsync(partnerKey: null, ct);
            return contacts.Sum(c => c.UnreadCount);
        }

        private async Task EnsureChatAccessAsync(CancellationToken ct)
        {
            if (_currentUser.UserId == Guid.Empty)
                throw new UnauthorizedAccessException("Oturum gerekli.");

            if (!await _userAccess.CanUseMobileOperationsChatAsync(ct))
                throw new UnauthorizedAccessException("Sohbet için geçerli oturum gerekli.");
        }

        private sealed record ContactRow(
            Guid UserId,
            string FullName,
            Guid TenantId,
            bool IsGaManagement,
            string? BadgeLabel,
            string? CompanyName);

        private async Task<List<ContactRow>> LoadEligibleContactsAsync(
            Guid meId,
            bool meIsGa,
            PartnerDefinition? partnerFilter,
            CancellationToken ct)
        {
            var tenantNames = await _context.Tenants
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(t => !t.IsDeleted)
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

            var users = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.FieldWorkerProfile!)
                    .ThenInclude(f => f.Projects)
                .Where(u => !u.IsDeleted && u.IsActive && u.Id != meId)
                .ToListAsync(ct);

            var meTenantId = _currentUser.TenantId;
            var result = new List<ContactRow>();

            foreach (var user in users)
            {
                var roles = user.UserRoles
                    .Where(ur => ur.Role != null && !ur.Role.IsDeleted)
                    .Select(ur => ur.Role!.Name)
                    .ToList();
                var isGa = DirectChatRules.IsGaManagementUser(user, roles);

                if (!DirectChatRules.CanUsersChat(
                        meId, user.Id, meTenantId, user.TenantId, meIsGa, isGa))
                    continue;

                if (meIsGa)
                {
                    var projectNames = user.FieldWorkerProfile?.Projects?
                        .Where(p => !p.IsDeleted)
                        .Select(p => p.Name)
                        .ToList() ?? new List<string>();
                    if (user.FieldWorkerProfile != null &&
                        !string.IsNullOrWhiteSpace(user.FieldWorkerProfile.ProjectName))
                        projectNames.Add(user.FieldWorkerProfile.ProjectName);

                    if (!isGa && !DirectChatRules.UserMatchesPartnerFilter(user, partnerFilter, projectNames))
                        continue;
                }

                tenantNames.TryGetValue(user.TenantId, out var companyName);
                result.Add(new ContactRow(
                    user.Id,
                    user.FullName,
                    user.TenantId,
                    isGa,
                    isGa ? DirectChatRules.GaManagementBadge : null,
                    isGa ? null : companyName));
            }

            return result;
        }

        private async Task<ContactRow?> LoadUserContactAsync(Guid userId, CancellationToken ct)
        {
            var meIsGa = await _userAccess.IsSuperAdminAsync(ct);
            var contacts = await LoadEligibleContactsAsync(_currentUser.UserId, meIsGa, partnerFilter: null, ct);
            return contacts.FirstOrDefault(c => c.UserId == userId);
        }

        private async Task EnsureCanMessageAsync(Guid targetUserId, CancellationToken ct)
        {
            var meIsGa = await _userAccess.IsSuperAdminAsync(ct);
            var target = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == targetUserId && !u.IsDeleted && u.IsActive, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var targetRoles = target.UserRoles
                .Where(ur => ur.Role != null && !ur.Role.IsDeleted)
                .Select(ur => ur.Role!.Name)
                .ToList();
            var targetIsGa = DirectChatRules.IsGaManagementUser(target, targetRoles);

            if (!DirectChatRules.CanUsersChat(
                    _currentUser.UserId,
                    targetUserId,
                    _currentUser.TenantId,
                    target.TenantId,
                    meIsGa,
                    targetIsGa))
                throw new UnauthorizedAccessException("Bu kullanıcıyla mesajlaşamazsınız.");
        }

        private static (Guid UserOneId, Guid UserTwoId) CanonicalPair(Guid a, Guid b) =>
            a.CompareTo(b) < 0 ? (a, b) : (b, a);

        private async Task<OfficeDirectConversation> GetOrCreateConversationAsync(
            Guid userA, Guid userB, CancellationToken ct)
        {
            var (one, two) = CanonicalPair(userA, userB);
            var meIsGa = await _userAccess.IsSuperAdminAsync(ct);

            var existing = await _context.OfficeDirectConversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c =>
                    !c.IsDeleted &&
                    c.UserOneId == one &&
                    c.UserTwoId == two, ct);

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

            var convTenantId = _currentUser.TenantId != Guid.Empty
                ? _currentUser.TenantId
                : (other.TenantId != Guid.Empty ? other.TenantId : me.TenantId);

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

            var otherId = conv.UserOneId == me ? conv.UserTwoId : conv.UserOneId;
            await EnsureCanMessageAsync(otherId, ct);
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
            var conv = await GetConversationEntityAsync(conversationId, ct)
                ?? throw new KeyNotFoundException("Konuşma bulunamadı.");

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

            var otherUserId = conv.UserOneId == _currentUser.UserId ? conv.UserTwoId : conv.UserOneId;
            var otherLastRead = await _context.OfficeDirectReadStates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r =>
                    r.OfficeDirectConversationId == conversationId &&
                    r.UserId == otherUserId &&
                    !r.IsDeleted)
                .Select(r => (DateTime?)r.LastReadAt)
                .FirstOrDefaultAsync(ct) ?? DateTime.MinValue;

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
                IsReadByOther = m.SenderUserId == me && otherLastRead >= m.SentAt,
            }).ToList();
        }

        private async Task<OfficeDirectMessageDto> MapMessageAsync(
            OfficeDirectMessage m, OfficeDirectConversation conv, Guid viewerUserId, CancellationToken ct)
        {
            var name = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == m.SenderUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct) ?? "Kullanıcı";

            var otherUserId = conv.UserOneId == viewerUserId ? conv.UserTwoId : conv.UserOneId;
            var otherLastRead = await _context.OfficeDirectReadStates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r =>
                    r.OfficeDirectConversationId == conv.Id &&
                    r.UserId == otherUserId &&
                    !r.IsDeleted)
                .Select(r => (DateTime?)r.LastReadAt)
                .FirstOrDefaultAsync(ct) ?? DateTime.MinValue;

            return new OfficeDirectMessageDto
            {
                Id = m.Id,
                ConversationId = m.OfficeDirectConversationId,
                SenderUserId = m.SenderUserId,
                SenderName = name,
                IsMine = m.SenderUserId == viewerUserId,
                Body = m.Body,
                SentAt = m.SentAt,
                ClientMessageId = m.ClientMessageId,
                IsReadByOther = m.SenderUserId == viewerUserId && otherLastRead >= m.SentAt,
            };
        }

        private static OfficeDirectConversationDto MapToLegacyDto(DirectContactDto c) => new()
        {
            Id = c.ConversationId,
            OtherUserId = c.UserId,
            OtherUserName = c.FullName,
            LastMessageAt = c.LastMessageAt,
            LastMessagePreview = c.LastMessagePreview,
            UnreadCount = c.UnreadCount,
            IsGaManagement = c.IsGaManagement,
            BadgeLabel = c.BadgeLabel,
            CompanyName = c.CompanyName,
        };

        private static string? Truncate(string? text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= max ? text : text[..max] + "…";
        }
    }
}
