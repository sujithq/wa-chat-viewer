using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using WhatsAppViewer.Models;

namespace WhatsAppViewer.Services;

public class WhatsAppChatParser
{
    private const long DefaultMaxChatTextBytes = 25L * 1024 * 1024;
    private const long DefaultMaxMediaFileBytes = 150L * 1024 * 1024;
    private const long DefaultMaxTotalExtractedBytes = 768L * 1024 * 1024;
    private const int DefaultMaxMediaFiles = 1000;

    private readonly long _maxChatTextBytes;
    private readonly long _maxMediaFileBytes;
    private readonly long _maxTotalExtractedBytes;
    private readonly int _maxMediaFiles;

    private static readonly string[] PreferredChatFileNames =
    {
        "_chat.txt",
        "chat.txt"
    };

    private static readonly string[] PreferredChatFilePrefixes =
    {
        "WhatsApp Chat with"
    };

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

    public WhatsAppChatParser(
        long maxChatTextBytes = DefaultMaxChatTextBytes,
        long maxMediaFileBytes = DefaultMaxMediaFileBytes,
        long maxTotalExtractedBytes = DefaultMaxTotalExtractedBytes,
        int maxMediaFiles = DefaultMaxMediaFiles)
    {
        if (maxChatTextBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChatTextBytes));
        if (maxMediaFileBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMediaFileBytes));
        if (maxTotalExtractedBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTotalExtractedBytes));
        if (maxMediaFiles <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMediaFiles));

        _maxChatTextBytes = maxChatTextBytes;
        _maxMediaFileBytes = maxMediaFileBytes;
        _maxTotalExtractedBytes = maxTotalExtractedBytes;
        _maxMediaFiles = maxMediaFiles;
    }

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
        // O(1) exact match — the dictionary uses OrdinalIgnoreCase, so this handles both
        // case-exact and case-insensitive full-path lookups in a single operation.
        if (media.TryGetValue(fileName, out _))
            return fileName;

        // Match by base filename alone when the result is unambiguous.
        var filenameMatches = media.Keys
            .Where(k => string.Equals(Path.GetFileName(k), fileName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        if (filenameMatches.Count == 1)
            return filenameMatches[0];

        return null;
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
        var chatCandidates = new List<ZipArchiveEntry>();
        long totalExtractedBytes = 0;
        var mediaCount = 0;

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var name = entry.Name;
            var ext = Path.GetExtension(name).ToLowerInvariant();

            if (string.Equals(ext, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                chatCandidates.Add(entry);
                continue;
            }

            if (!IsMediaFile(ext))
                continue;

            mediaCount++;
            if (mediaCount > _maxMediaFiles)
                throw new InvalidDataException($"The ZIP contains more than {_maxMediaFiles} media files.");

            ValidateEntryLimit(entry, _maxMediaFileBytes, $"Media file '{entry.FullName}'");

            await using var stream = entry.Open();
            var data = await ReadEntryBytesAsync(stream, _maxMediaFileBytes, $"Media file '{entry.FullName}'");
            ValidateTotalExtractionLimit(totalExtractedBytes, data.LongLength, _maxTotalExtractedBytes);
            totalExtractedBytes += data.LongLength;

            var mimeType = GetMimeType(ext);
            mediaFiles[entry.FullName] = (data, mimeType);
        }

        var selectedChat = SelectChatEntry(chatCandidates);
        if (selectedChat == null)
            throw new InvalidOperationException("No supported chat text file found in the ZIP archive. Expected '_chat.txt', 'chat.txt', or a filename starting with 'WhatsApp Chat with'.");

        ValidateEntryLimit(selectedChat, _maxChatTextBytes, $"Chat file '{selectedChat.FullName}'");

        await using var chatStream = selectedChat.Open();
        var chatBytes = await ReadEntryBytesAsync(chatStream, _maxChatTextBytes, $"Chat file '{selectedChat.FullName}'");
        ValidateTotalExtractionLimit(totalExtractedBytes, chatBytes.LongLength, _maxTotalExtractedBytes);
        totalExtractedBytes += chatBytes.LongLength;
        var chatText = Encoding.UTF8.GetString(chatBytes);

        return Parse(chatText, mediaFiles);
    }

    private static ZipArchiveEntry? SelectChatEntry(IReadOnlyList<ZipArchiveEntry> chatCandidates)
    {
        foreach (var preferredName in PreferredChatFileNames)
        {
            var preferred = chatCandidates.FirstOrDefault(entry =>
                string.Equals(entry.Name, preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred != null)
                return preferred;
        }

        var prefixed = chatCandidates
            .Where(entry => PreferredChatFilePrefixes.Any(prefix =>
                entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Name.Length)
            .ThenBy(entry => entry.FullName.Length)
            .FirstOrDefault();

        return prefixed;
    }

    private static async Task<byte[]> ReadEntryBytesAsync(Stream stream, long maxBytes, string label)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
                break;

            totalBytes += read;
            if (totalBytes > maxBytes)
                throw new InvalidDataException($"{label} exceeds the allowed size of {FormatSize(maxBytes)}.");

            await ms.WriteAsync(buffer, 0, read);
        }

        return ms.ToArray();
    }

    private static void ValidateEntryLimit(ZipArchiveEntry entry, long maxBytes, string label)
    {
        if (entry.Length > maxBytes)
            throw new InvalidDataException($"{label} exceeds the allowed size of {FormatSize(maxBytes)}.");
    }

    private static void ValidateTotalExtractionLimit(long currentBytes, long nextEntryBytes, long maxTotalBytes)
    {
        if (nextEntryBytes > maxTotalBytes - currentBytes)
            throw new InvalidDataException($"ZIP content exceeds the allowed extracted size of {FormatSize(maxTotalBytes)}.");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} byte{(bytes == 1 ? string.Empty : "s")}";

        if (bytes < 1024L * 1024L)
        {
            var kiloBytes = bytes / 1024d;
            return $"{kiloBytes:0.#} KB";
        }
        var megaBytes = bytes / (1024d * 1024d);
        return $"{megaBytes:0.#} MB";
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
