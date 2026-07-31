namespace GA.Application.Features.OfficeChat.DTOs
{
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
    }

    public class OfficeDirectConversationDto
    {
        public Guid? Id { get; set; }
        public Guid OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
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
}
