namespace GA.Application.Features.Chat.DTOs
{
    public class ChatMessageDto
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderUserId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public bool IsFromFieldWorker { get; set; }
        public string Body { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string? ClientMessageId { get; set; }
    }

    public class ConversationDto
    {
        public Guid Id { get; set; }
        public Guid FieldWorkerUserId { get; set; }
        public string FieldWorkerName { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public string? LastMessagePreview { get; set; }
        public int UnreadCount { get; set; }
    }

    public class MyConversationResponse
    {
        public Guid Id { get; set; }
        public string CounterpartyLabel { get; set; } = "Operasyon";
        public int UnreadCount { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class SendMessageRequest
    {
        public string Body { get; set; } = string.Empty;
        public string? ClientMessageId { get; set; }
    }
}
