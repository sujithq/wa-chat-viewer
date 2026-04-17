using WhatsAppViewer.Models;

namespace WhatsAppViewer.Services;

public sealed record ChatSearchMatch(int MessageIndex, int Start, int Length);

public sealed class ChatSearchService
{
    public IReadOnlyList<ChatSearchMatch> FindMatches(
        IReadOnlyList<ChatMessage> messages,
        string query,
        bool caseSensitive = false,
        bool includeSystemMessages = false)
    {
        var results = new List<ChatSearchMatch>();

        if (messages.Count == 0 || string.IsNullOrWhiteSpace(query))
            return results;

        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (var i = 0; i < messages.Count; i++)
        {
            var message = messages[i];
            if (!includeSystemMessages && message.IsSystem)
                continue;

            if (string.IsNullOrEmpty(message.Content))
                continue;

            var startIndex = 0;
            while (startIndex < message.Content.Length)
            {
                var matchIndex = message.Content.IndexOf(query, startIndex, comparison);
                if (matchIndex < 0)
                    break;

                results.Add(new ChatSearchMatch(i, matchIndex, query.Length));
                startIndex = matchIndex + query.Length;
            }
        }

        return results;
    }
}
