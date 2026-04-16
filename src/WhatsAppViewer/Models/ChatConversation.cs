namespace WhatsAppViewer.Models;

public class ChatConversation
{
    public string Title { get; set; } = "WhatsApp Conversation";
    public List<ChatMessage> Messages { get; set; } = new();
    public List<string> Participants { get; set; } = new();
    public Dictionary<string, (byte[] Data, string MimeType)> MediaFiles { get; set; } = new();
}
