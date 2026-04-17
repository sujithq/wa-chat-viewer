using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using WhatsAppViewer.Models;

namespace WhatsAppViewer.Services;

public class WhatsAppChatParser
{
    // Android format: MM/DD/YYYY, HH:MM - Sender: message
    // Android format (24h): DD/MM/YYYY, HH:MM - Sender: message
    // iOS format: [DD/MM/YYYY, HH:MM:SS] Sender: message
    // Android format with AM/PM: M/D/YY, H:MM AM - Sender: message

    private static readonly Regex[] MessagePatterns = new[]
    {
        // iOS: [01/23/2024, 12:34:56] Sender: Message
        new Regex(@"^\[(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\]\s*([^:]+?):\s*(.*)", RegexOptions.Compiled),
        // Android: 01/23/24, 12:34 AM - Sender: Message  OR  23/01/2024, 12:34 - Sender: Message
        new Regex(@"^(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\s*-\s*([^:]+?):\s*(.*)", RegexOptions.Compiled),
        // Android with dash in date: 01-23-24, 12:34 AM - Sender: Message
        new Regex(@"^(\d{1,2}-\d{1,2}-\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\s*-\s*([^:]+?):\s*(.*)", RegexOptions.Compiled),
    };

    private static readonly Regex[] SystemMessagePatterns = new[]
    {
        // iOS system: [01/23/2024, 12:34:56] System message (no colon after sender)
        new Regex(@"^\[(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\]\s*(.*)", RegexOptions.Compiled),
        // Android system: 01/23/24, 12:34 AM - System message
        new Regex(@"^(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\s*-\s*(.*)", RegexOptions.Compiled),
        // Android with dash in date: 01-23-24, 12:34 AM - System message
        new Regex(@"^(\d{1,2}-\d{1,2}-\d{2,4}),\s*(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\s*-\s*(.*)", RegexOptions.Compiled),
    };

    private static readonly Regex MediaOmittedPattern = new Regex(@"<Media omitted>|<[^>]+ omitted>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AttachedFilePattern = new Regex(@"^(.+?)\s*\(file attached\)$", RegexOptions.Compiled);
    private static readonly Regex AttachedTagPattern = new Regex(@"^[\u200E\u200F\uFEFF\s]*<attached:\s*(.+?)>[\u200E\u200F\uFEFF\s]*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ChatConversation Parse(string chatText, Dictionary<string, (byte[] Data, string MimeType)>? mediaFiles = null)
    {
        var conversation = new ChatConversation();
        if (mediaFiles != null)
            conversation.MediaFiles = mediaFiles;

        var lines = chatText.Split('\n');
        ChatMessage? currentMessage = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');
            line = TrimLeadingUnicodeMarkers(line);
            if (string.IsNullOrEmpty(line)) continue;

            // Try to parse as a new message
            var parsed = TryParseMessageLine(line);
            if (parsed != null)
            {
                if (currentMessage != null)
                    conversation.Messages.Add(currentMessage);
                currentMessage = parsed;
            }
            else if (currentMessage != null)
            {
                // Continuation of previous message
                currentMessage.Content += "\n" + line;
            }
        }

        if (currentMessage != null)
            conversation.Messages.Add(currentMessage);

        // Resolve media references
        foreach (var msg in conversation.Messages)
        {
            if (msg.Type != MessageType.MediaOmitted && msg.MediaFileName != null && mediaFiles != null)
            {
                var key = FindMediaKey(mediaFiles, msg.MediaFileName);
                if (key != null)
                {
                    msg.MediaData = mediaFiles[key].Data;
                    msg.MediaMimeType = mediaFiles[key].MimeType;
                }
            }
        }

        // Collect participants
        conversation.Participants = conversation.Messages
            .Where(m => !m.IsSystem && !string.IsNullOrEmpty(m.Sender))
            .Select(m => m.Sender)
            .Distinct()
            .ToList();

        // Try to set title from first system message
        var titleMsg = conversation.Messages.FirstOrDefault(m => m.IsSystem &&
            (m.Content.Contains("created group") || m.Content.Contains("Messages and calls are end-to-end encrypted")));
        if (titleMsg != null && titleMsg.Content.Contains("created group"))
        {
            // e.g. "John created group 'My Group'"
            var match = Regex.Match(titleMsg.Content, @"[""'](.+?)[""']");
            if (match.Success)
                conversation.Title = match.Groups[1].Value;
        }

        return conversation;
    }

    private static string TrimLeadingUnicodeMarkers(string value)
    {
        var index = 0;
        while (index < value.Length)
        {
            var ch = value[index];
            if (ch is '\u200E' or '\u200F' or '\uFEFF')
            {
                index++;
                continue;
            }

            break;
        }

        return index == 0 ? value : value[index..];
    }

    private ChatMessage? TryParseMessageLine(string line)
    {
        // Try each message pattern
        for (int i = 0; i < MessagePatterns.Length; i++)
        {
            var match = MessagePatterns[i].Match(line);
            if (match.Success)
            {
                var dateStr = match.Groups[1].Value;
                var timeStr = match.Groups[2].Value;
                var sender = match.Groups[3].Value.Trim();
                var content = match.Groups[4].Value;

                if (!TryParseDateTime(dateStr, timeStr, out var dt))
                    dt = DateTime.MinValue;

                return CreateMessage(dt, sender, content, false);
            }
        }

        // Try system message patterns
        for (int i = 0; i < SystemMessagePatterns.Length; i++)
        {
            var match = SystemMessagePatterns[i].Match(line);
            if (match.Success)
            {
                var dateStr = match.Groups[1].Value;
                var timeStr = match.Groups[2].Value;
                var content = match.Groups[3].Value;

                if (!TryParseDateTime(dateStr, timeStr, out var dt))
                    dt = DateTime.MinValue;

                return CreateMessage(dt, string.Empty, content, true);
            }
        }

        return null;
    }

    private ChatMessage CreateMessage(DateTime timestamp, string sender, string content, bool isSystem)
    {
        var msg = new ChatMessage
        {
            Timestamp = timestamp,
            Sender = sender,
            IsSystem = isSystem,
            Type = isSystem ? MessageType.System : MessageType.Text
        };

        if (isSystem)
        {
            msg.Content = content;
            return msg;
        }

        // Check for media omitted
        if (MediaOmittedPattern.IsMatch(content))
        {
            msg.Type = MessageType.MediaOmitted;
            msg.Content = content;
            return msg;
        }

        // Check for attached file
        var attachedMatch = AttachedFilePattern.Match(content);
        if (attachedMatch.Success)
        {
            var fileName = attachedMatch.Groups[1].Value.Trim();
            msg.MediaFileName = fileName;
            msg.Content = fileName;
            msg.Type = DetermineMediaType(fileName);
            return msg;
        }

        // iOS-style attachment marker: <attached: filename.ext>
        var attachedTagMatch = AttachedTagPattern.Match(content);
        if (attachedTagMatch.Success)
        {
            var fileName = attachedTagMatch.Groups[1].Value.Trim();
            msg.MediaFileName = fileName;
            msg.Content = fileName;
            msg.Type = DetermineMediaType(fileName);
            return msg;
        }

        msg.Content = content;
        return msg;
    }

    private static MessageType DetermineMediaType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => MessageType.Image,
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".3gp" or ".webm" => MessageType.Video,
            ".mp3" or ".ogg" or ".m4a" or ".opus" or ".aac" or ".wav" => MessageType.Audio,
            _ => MessageType.Document
        };
    }

    private static string? FindMediaKey(Dictionary<string, (byte[] Data, string MimeType)> media, string fileName)
    {
        // Exact match
        if (media.ContainsKey(fileName)) return fileName;

        // Case-insensitive match
        var lower = fileName.ToLowerInvariant();
        return media.Keys.FirstOrDefault(k => k.ToLowerInvariant() == lower ||
                                               Path.GetFileName(k).ToLowerInvariant() == lower);
    }

    private static bool TryParseDateTime(string dateStr, string timeStr, out DateTime result)
    {
        result = DateTime.MinValue;
        var combined = $"{dateStr} {timeStr.Trim()}";

        // Normalize date separators
        combined = combined.Replace('-', '/');

        string[] formats =
        {
            "M/d/yyyy h:mm tt",
            "M/d/yyyy h:mm:ss tt",
            "M/d/yyyy HH:mm",
            "M/d/yyyy HH:mm:ss",
            "d/M/yyyy h:mm tt",
            "d/M/yyyy h:mm:ss tt",
            "d/M/yyyy HH:mm",
            "d/M/yyyy HH:mm:ss",
            "M/d/yy h:mm tt",
            "M/d/yy h:mm:ss tt",
            "M/d/yy HH:mm",
            "M/d/yy HH:mm:ss",
            "d/M/yy h:mm tt",
            "d/M/yy h:mm:ss tt",
            "d/M/yy HH:mm",
            "d/M/yy HH:mm:ss",
            "yyyy/M/d HH:mm",
            "yyyy/M/d HH:mm:ss",
        };

        return DateTime.TryParseExact(combined.Trim(), formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out result);
    }

    public async Task<ChatConversation> ParseZipAsync(Stream zipStream)
    {
        var mediaFiles = new Dictionary<string, (byte[] Data, string MimeType)>(StringComparer.OrdinalIgnoreCase);
        string? chatText = null;

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            var name = entry.Name;
            var ext = Path.GetExtension(name).ToLowerInvariant();

            // Look for the chat text file
            if (name.Equals("_chat.txt", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                chatText = await reader.ReadToEndAsync();
            }
            else if (IsMediaFile(ext))
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var mimeType = GetMimeType(ext);
                mediaFiles[name] = (ms.ToArray(), mimeType);
            }
        }

        if (chatText == null)
            throw new InvalidOperationException("No chat text file found in the ZIP archive. Expected a file named '_chat.txt' or a .txt file.");

        return Parse(chatText, mediaFiles);
    }

    private static bool IsMediaFile(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => true,
        ".mp4" or ".mov" or ".avi" or ".mkv" or ".3gp" or ".webm" => true,
        ".mp3" or ".ogg" or ".m4a" or ".opus" or ".aac" or ".wav" => true,
        ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".zip" => true,
        _ => false
    };

    private static string GetMimeType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".3gp" => "video/3gpp",
        ".mp3" => "audio/mpeg",
        ".ogg" => "audio/ogg",
        ".m4a" => "audio/mp4",
        ".opus" => "audio/opus",
        ".aac" => "audio/aac",
        ".wav" => "audio/wav",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}
