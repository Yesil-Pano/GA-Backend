namespace GA.Application.Features.OfficeChat.DTOs
{
    public class DirectContactDto
    {
        public Guid? ConversationId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsGaManagement { get; set; }
        public string? BadgeLabel { get; set; }
        public string? CompanyName { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
    }

    public class OfficeDirectMessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string? ClientMessageId { get; set; }
        /// <summary>Karşı taraf bu mesajı okudu mu (mavi tik).</summary>
        public bool IsReadByOther { get; set; }
    }

    /// <summary>Backward-compatible alias.</summary>
    public class OfficeDirectConversationDto
    {
        public Guid? Id { get; set; }
        public Guid OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
        public bool IsGaManagement { get; set; }
        public string? BadgeLabel { get; set; }
        public string? CompanyName { get; set; }
    }

    public class StartOfficeConversationRequest
    {
        public Guid TargetUserId { get; set; }
    }

    public class SendOfficeMessageRequest
    {
        public string Body { get; set; } = string.Empty;
        public string? ClientMessageId { get; set; }
    }

    public class RegisterWebPushSubscriptionRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }

    public class WebPushVapidPublicKeyResponse
    {
        public string PublicKey { get; set; } = string.Empty;
    }
}
