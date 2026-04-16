namespace WhatsAppViewer.Models;

public class ChatMessage
{
    public DateTime Timestamp { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    public string? MediaFileName { get; set; }
    public byte[]? MediaData { get; set; }
    public string? MediaMimeType { get; set; }
}

public enum MessageType
{
    Text,
    Image,
    Video,
    Audio,
    Document,
    MediaOmitted,
    System
}
