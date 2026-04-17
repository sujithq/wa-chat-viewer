using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhatsAppViewer.Models;
using WhatsAppViewer.Services;

namespace WhatsAppViewer.Tests;

[TestClass]
public class ChatSearchServiceTests
{
    private readonly ChatSearchService _service = new();

    [TestMethod]
    public void FindMatches_EmptyQuery_ReturnsNoMatches()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "Hello" }
        };

        var matches = _service.FindMatches(messages, string.Empty);

        Assert.AreEqual(0, matches.Count);
    }

    [TestMethod]
    public void FindMatches_CaseInsensitive_FindsMatchesAcrossMessages()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "Hello world" },
            new ChatMessage { Content = "HELLO again" }
        };

        var matches = _service.FindMatches(messages, "hello", caseSensitive: false);

        Assert.AreEqual(2, matches.Count);
        Assert.AreEqual(0, matches[0].MessageIndex);
        Assert.AreEqual(0, matches[0].Start);
        Assert.AreEqual(1, matches[1].MessageIndex);
        Assert.AreEqual(0, matches[1].Start);
    }

    [TestMethod]
    public void FindMatches_CaseSensitive_RespectsCase()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "Hello" },
            new ChatMessage { Content = "hello" }
        };

        var matches = _service.FindMatches(messages, "hello", caseSensitive: true);

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(1, matches[0].MessageIndex);
    }

    [TestMethod]
    public void FindMatches_FindsMultipleNonOverlappingMatchesInSingleMessage()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "test test test" }
        };

        var matches = _service.FindMatches(messages, "test");

        Assert.AreEqual(3, matches.Count);
        Assert.AreEqual(0, matches[0].Start);
        Assert.AreEqual(5, matches[1].Start);
        Assert.AreEqual(10, matches[2].Start);
    }

    [TestMethod]
    public void FindMatches_ExcludesSystemMessagesByDefault()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "Alice joined", IsSystem = true },
            new ChatMessage { Content = "joined now", IsSystem = false }
        };

        var matches = _service.FindMatches(messages, "joined");

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(1, matches[0].MessageIndex);
    }

    [TestMethod]
    public void FindMatches_IncludesSystemMessagesWhenRequested()
    {
        var messages = new[]
        {
            new ChatMessage { Content = "Alice joined", IsSystem = true }
        };

        var matches = _service.FindMatches(messages, "joined", includeSystemMessages: true);

        Assert.AreEqual(1, matches.Count);
        Assert.AreEqual(0, matches[0].MessageIndex);
    }
}
