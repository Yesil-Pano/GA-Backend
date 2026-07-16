using GA.Application.Features.Auth;
using GA.Application.Features.Chat.DTOs;
using GA.Application.Features.Partners;
using GA.Core.Domain.Entities;
using GA.Core.Interfaces;
using GA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GA.Application.Features.Chat
{
    public interface IChatService
    {
        Task<MyConversationResponse> GetMyConversationAsync(int take, CancellationToken ct = default);
        Task<List<ConversationDto>> ListConversationsAsync(string? partnerKey, CancellationToken ct = default);
        Task<List<ChatMessageDto>> GetMessagesAsync(Guid conversationId, DateTime? before, int take, CancellationToken ct = default);
        Task<(ChatMessageDto Message, Guid TenantId, Guid FieldWorkerUserId)> SendMessageAsync(
            Guid? conversationId, SendMessageRequest request, CancellationToken ct = default);
        Task<(Guid ConversationId, Guid UserId, DateTime LastReadAt)> MarkReadAsync(
            Guid conversationId, CancellationToken ct = default);
        Task<int> GetMyUnreadTotalAsync(CancellationToken ct = default);
    }

    public class ChatService : IChatService
    {
        private static readonly Guid YesilPanoTenantId = Guid.Parse("475e2c63-5dca-41c8-ba0e-fd86917f32f0");
        private static readonly Guid TrugoTenantId = Guid.Parse("c92cc573-957b-4862-8ae7-ff380efd15ce");

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUser;
        private readonly IUserAccessService _userAccess;

        public ChatService(
            ApplicationDbContext context,
            ICurrentUserService currentUser,
            IUserAccessService userAccess)
        {
            _context = context;
            _currentUser = currentUser;
            _userAccess = userAccess;
        }

        public async Task<MyConversationResponse> GetMyConversationAsync(int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            EnsureAuthenticated();

            if (!await _userAccess.IsFieldWorkerOnlyForChatAsync(ct))
                throw new InvalidOperationException("Bu uç nokta yalnızca saha personeli içindir.");

            var conv = await GetOrCreateFieldConversationAsync(_currentUser.UserId, ct);
            var unread = await CountUnreadAsync(conv.Id, _currentUser.UserId, ct);
            var messages = await LoadMessagesAsync(conv.Id, before: null, take, ct);

            return new MyConversationResponse
            {
                Id = conv.Id,
                CounterpartyLabel = "Operasyon",
                UnreadCount = unread,
                Messages = messages,
            };
        }

        public async Task<List<ConversationDto>> ListConversationsAsync(
            string? partnerKey, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            if (!await _userAccess.CanAccessOfficeChatInboxAsync(ct))
                throw new UnauthorizedAccessException("Bu konuşma listesine erişim yetkiniz yok.");

            var tenantId = _currentUser.TenantId;
            var isSuperAdmin = await _userAccess.IsSuperAdminAsync(ct);

            var workersQuery = _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.FieldWorkerProfile)
                    .ThenInclude(f => f!.Projects)
                .Where(u => !u.IsDeleted && u.FieldWorkerProfile != null && !u.FieldWorkerProfile.IsDeleted &&
                            (isSuperAdmin ||
                             u.TenantId == tenantId ||
                             (tenantId == TrugoTenantId && u.TenantId == YesilPanoTenantId) ||
                             (tenantId == YesilPanoTenantId && u.TenantId == TrugoTenantId)));

            var workers = await workersQuery
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.TenantId,
                    u.CustomerId,
                    ProjectNames = u.FieldWorkerProfile!.Projects.Any()
                        ? u.FieldWorkerProfile.Projects.Select(p => p.Name).ToList()
                        : (string.IsNullOrWhiteSpace(u.FieldWorkerProfile.ProjectName)
                            ? new List<string>()
                            : new List<string> { u.FieldWorkerProfile.ProjectName! }),
                })
                .ToListAsync(ct);

            if (isSuperAdmin)
            {
                var partner = PartnerCatalog.Find(partnerKey) ?? PartnerCatalog.Trugo;
                workers = workers
                    .Where(w => PartnerCatalog.MatchesTeam(partner, w.TenantId, w.ProjectNames))
                    .ToList();
            }

            var workerIds = workers.Select(w => w.Id).ToList();
            var existing = await _context.Conversations
                .IgnoreQueryFilters()
                .Where(c => !c.IsDeleted && workerIds.Contains(c.FieldWorkerUserId))
                .ToListAsync(ct);
            var byWorker = existing.ToDictionary(c => c.FieldWorkerUserId);

            foreach (var w in workers)
            {
                if (byWorker.ContainsKey(w.Id)) continue;
                var conv = new Conversation
                {
                    FieldWorkerUserId = w.Id,
                    TenantId = w.TenantId,
                    CustomerId = w.CustomerId,
                };
                _context.Conversations.Add(conv);
                byWorker[w.Id] = conv;
            }
            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync(ct);

            var convIds = byWorker.Values.Select(c => c.Id).ToList();
            var lastMessages = await _context.ChatMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => convIds.Contains(m.ConversationId) && !m.IsDeleted)
                .GroupBy(m => m.ConversationId)
                .Select(g => new
                {
                    ConversationId = g.Key,
                    Body = g.OrderByDescending(x => x.SentAt).Select(x => x.Body).FirstOrDefault(),
                    SentAt = g.Max(x => x.SentAt),
                })
                .ToListAsync(ct);
            var lastByConv = lastMessages.ToDictionary(x => x.ConversationId);

            var result = new List<ConversationDto>();
            foreach (var w in workers)
            {
                var conv = byWorker[w.Id];
                lastByConv.TryGetValue(conv.Id, out var last);
                result.Add(new ConversationDto
                {
                    Id = conv.Id,
                    FieldWorkerUserId = w.Id,
                    FieldWorkerName = w.FullName,
                    LastMessageAt = last?.SentAt ?? conv.LastMessageAt,
                    LastMessagePreview = Truncate(last?.Body, 80),
                    UnreadCount = await CountUnreadAsync(conv.Id, _currentUser.UserId, ct),
                });
            }

            return result
                .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(c => c.FieldWorkerName)
                .ToList();
        }

        public async Task<List<ChatMessageDto>> GetMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct = default)
        {
            take = Math.Clamp(take, 1, 100);
            EnsureAuthenticated();
            await EnsureCanAccessConversationAsync(conversationId, ct);
            return await LoadMessagesAsync(conversationId, before, take, ct);
        }

        public async Task<(ChatMessageDto Message, Guid TenantId, Guid FieldWorkerUserId)> SendMessageAsync(
            Guid? conversationId, SendMessageRequest request, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var body = (request.Body ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(body))
                throw new InvalidOperationException("Mesaj boş olamaz.");
            if (body.Length > 2000)
                throw new InvalidOperationException("Mesaj en fazla 2000 karakter olabilir.");

            var isFieldOnly = await _userAccess.IsFieldWorkerOnlyForChatAsync(ct);
            Conversation conv;

            if (isFieldOnly)
            {
                conv = await GetOrCreateFieldConversationAsync(_currentUser.UserId, ct);
                if (conversationId.HasValue && conversationId.Value != conv.Id)
                    throw new UnauthorizedAccessException("Bu konuşmaya mesaj gönderemezsiniz.");
            }
            else
            {
                if (!conversationId.HasValue)
                    throw new InvalidOperationException("Konuşma kimliği zorunludur.");
                conv = await GetConversationEntityAsync(conversationId.Value, ct)
                    ?? throw new KeyNotFoundException("Konuşma bulunamadı.");
                await EnsureCanAccessConversationAsync(conv.Id, ct);
            }

            if (!string.IsNullOrWhiteSpace(request.ClientMessageId))
            {
                var existing = await _context.ChatMessages
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.ConversationId == conv.Id &&
                        m.SenderUserId == _currentUser.UserId &&
                        m.ClientMessageId == request.ClientMessageId &&
                        !m.IsDeleted, ct);

                if (existing != null)
                    return (await MapMessageAsync(existing, ct), conv.TenantId, conv.FieldWorkerUserId);
            }

            var msg = new ChatMessage
            {
                ConversationId = conv.Id,
                SenderUserId = _currentUser.UserId,
                Body = body,
                SentAt = DateTime.UtcNow,
                ClientMessageId = string.IsNullOrWhiteSpace(request.ClientMessageId)
                    ? null
                    : request.ClientMessageId.Trim(),
                TenantId = conv.TenantId,
                CustomerId = conv.CustomerId,
            };

            _context.ChatMessages.Add(msg);
            conv.LastMessageAt = msg.SentAt;
            conv.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            await UpsertReadStateAsync(conv.Id, _currentUser.UserId, msg.SentAt, ct);

            var dto = await MapMessageAsync(msg, ct);
            return (dto, conv.TenantId, conv.FieldWorkerUserId);
        }

        public async Task<(Guid ConversationId, Guid UserId, DateTime LastReadAt)> MarkReadAsync(
            Guid conversationId, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await EnsureCanAccessConversationAsync(conversationId, ct);
            var now = DateTime.UtcNow;
            await UpsertReadStateAsync(conversationId, _currentUser.UserId, now, ct);
            return (conversationId, _currentUser.UserId, now);
        }

        public async Task<int> GetMyUnreadTotalAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            if (await _userAccess.IsFieldWorkerOnlyForChatAsync(ct))
            {
                var conv = await _context.Conversations
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c =>
                        c.FieldWorkerUserId == _currentUser.UserId && !c.IsDeleted, ct);
                if (conv == null) return 0;
                return await CountUnreadAsync(conv.Id, _currentUser.UserId, ct);
            }

            var list = await ListConversationsAsync(partnerKey: null, ct);
            return list.Sum(c => c.UnreadCount);
        }

        private void EnsureAuthenticated()
        {
            if (_currentUser.UserId == Guid.Empty)
                throw new UnauthorizedAccessException("Oturum gerekli.");
        }

        // unused helper removed — IsFromFieldWorker now uses Conversation.FieldWorkerUserId

        private async Task<Conversation> GetOrCreateFieldConversationAsync(Guid fieldWorkerUserId, CancellationToken ct)
        {
            var existing = await _context.Conversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c =>
                    c.FieldWorkerUserId == fieldWorkerUserId && !c.IsDeleted, ct);

            if (existing != null) return existing;

            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == fieldWorkerUserId && !u.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Kullanıcı bulunamadı.");

            var conv = new Conversation
            {
                FieldWorkerUserId = fieldWorkerUserId,
                TenantId = user.TenantId,
                CustomerId = user.CustomerId,
            };
            _context.Conversations.Add(conv);
            await _context.SaveChangesAsync(ct);
            return conv;
        }

        private async Task<Conversation?> GetConversationEntityAsync(Guid id, CancellationToken ct) =>
            await _context.Conversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

        private bool CanOfficeAccessTenant(Guid conversationTenantId)
        {
            var tenantId = _currentUser.TenantId;
            if (tenantId == Guid.Empty) return true;
            if (conversationTenantId == tenantId) return true;
            if (tenantId == TrugoTenantId && conversationTenantId == YesilPanoTenantId) return true;
            if (tenantId == YesilPanoTenantId && conversationTenantId == TrugoTenantId) return true;
            return false;
        }

        private async Task EnsureCanAccessConversationAsync(Guid conversationId, CancellationToken ct)
        {
            var conv = await GetConversationEntityAsync(conversationId, ct)
                ?? throw new KeyNotFoundException("Konuşma bulunamadı.");

            if (await _userAccess.IsFieldWorkerOnlyForChatAsync(ct))
            {
                if (conv.FieldWorkerUserId != _currentUser.UserId)
                    throw new UnauthorizedAccessException("Bu konuşmaya erişemezsiniz.");
                return;
            }

            if (!await _userAccess.CanAccessOfficeChatInboxAsync(ct))
                throw new UnauthorizedAccessException("Bu konuşmaya erişim yetkiniz yok.");

            if (await _userAccess.IsSuperAdminAsync(ct))
                return;

            if (!CanOfficeAccessTenant(conv.TenantId))
                throw new UnauthorizedAccessException("Bu konuşmaya erişemezsiniz.");
        }

        private async Task<int> CountUnreadAsync(Guid conversationId, Guid userId, CancellationToken ct)
        {
            var lastRead = await _context.ChatReadStates
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.ConversationId == conversationId && r.UserId == userId && !r.IsDeleted)
                .Select(r => (DateTime?)r.LastReadAt)
                .FirstOrDefaultAsync(ct);

            var cutoff = lastRead ?? DateTime.MinValue;

            return await _context.ChatMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(m =>
                    m.ConversationId == conversationId &&
                    !m.IsDeleted &&
                    m.SenderUserId != userId &&
                    m.SentAt > cutoff, ct);
        }

        private async Task UpsertReadStateAsync(
            Guid conversationId, Guid userId, DateTime lastReadAt, CancellationToken ct)
        {
            var state = await _context.ChatReadStates
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r =>
                    r.ConversationId == conversationId &&
                    r.UserId == userId &&
                    !r.IsDeleted, ct);

            if (state == null)
            {
                var conv = await GetConversationEntityAsync(conversationId, ct);
                state = new ChatReadState
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    LastReadAt = lastReadAt,
                    TenantId = conv?.TenantId ?? _currentUser.TenantId,
                    CustomerId = conv?.CustomerId ?? _currentUser.CustomerId,
                };
                _context.ChatReadStates.Add(state);
            }
            else if (lastReadAt > state.LastReadAt)
            {
                state.LastReadAt = lastReadAt;
                state.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task<List<ChatMessageDto>> LoadMessagesAsync(
            Guid conversationId, DateTime? before, int take, CancellationToken ct)
        {
            var q = _context.ChatMessages
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId && !m.IsDeleted);

            if (before.HasValue)
                q = q.Where(m => m.SentAt < before.Value);

            var rows = await q
                .OrderByDescending(m => m.SentAt)
                .Take(take)
                .ToListAsync(ct);

            rows.Reverse();

            var fieldWorkerUserId = await _context.Conversations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == conversationId && !c.IsDeleted)
                .Select(c => c.FieldWorkerUserId)
                .FirstOrDefaultAsync(ct);

            var senderIds = rows.Select(m => m.SenderUserId).Distinct().ToList();
            var senders = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => senderIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FullName })
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

            // 1:1 sohbette "saha tarafı" = konuşmanın FieldWorkerUserId'si
            // (Admin'de FieldWorkerProfile olsa bile ofis mesajı sayılır)
            return rows.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderName = senders.GetValueOrDefault(m.SenderUserId, "Kullanıcı"),
                IsFromFieldWorker = m.SenderUserId == fieldWorkerUserId,
                Body = m.Body,
                SentAt = m.SentAt,
                ClientMessageId = m.ClientMessageId,
            }).ToList();
        }

        private async Task<ChatMessageDto> MapMessageAsync(ChatMessage m, CancellationToken ct)
        {
            var name = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == m.SenderUserId)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct) ?? "Kullanıcı";

            var fieldWorkerUserId = await _context.Conversations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == m.ConversationId && !c.IsDeleted)
                .Select(c => (Guid?)c.FieldWorkerUserId)
                .FirstOrDefaultAsync(ct);

            return new ChatMessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderUserId = m.SenderUserId,
                SenderName = name,
                IsFromFieldWorker = fieldWorkerUserId.HasValue && m.SenderUserId == fieldWorkerUserId.Value,
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
