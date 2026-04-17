using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhatsAppViewer.Models;
using WhatsAppViewer.Services;

namespace WhatsAppViewer.Tests;

[TestClass]
public class WhatsAppChatParserTests
{
    private readonly WhatsAppChatParser _parser = new();

    [TestMethod]
    public void Parse_AndroidFormat_ParsesMessages()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Hello!\n1/15/24, 9:31 AM - Bob: Hi there!";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(2, result.Messages.Count);
        Assert.AreEqual("Alice", result.Messages[0].Sender);
        Assert.AreEqual("Hello!", result.Messages[0].Content);
        Assert.AreEqual("Bob", result.Messages[1].Sender);
        Assert.AreEqual("Hi there!", result.Messages[1].Content);
    }

    [TestMethod]
    public void Parse_iOSFormat_ParsesMessages()
    {
        var chatText = "[15/01/2024, 09:30:00] Alice: Hello!\n[15/01/2024, 09:31:00] Bob: Hi there!";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(2, result.Messages.Count);
        Assert.AreEqual("Alice", result.Messages[0].Sender);
        Assert.AreEqual("Hello!", result.Messages[0].Content);
    }

    [TestMethod]
    public void Parse_SystemMessage_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:00 AM - Messages and calls are end-to-end encrypted.\n1/15/24, 9:01 AM - Alice: Hey!";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(2, result.Messages.Count);
        Assert.IsTrue(result.Messages[0].IsSystem);
        Assert.IsFalse(result.Messages[1].IsSystem);
    }

    [TestMethod]
    public void Parse_MediaOmitted_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: <Media omitted>";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageType.MediaOmitted, result.Messages[0].Type);
    }

    [TestMethod]
    public void Parse_AttachedImage_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: IMG-20240115-WA0001.jpg (file attached)";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageType.Image, result.Messages[0].Type);
        Assert.AreEqual("IMG-20240115-WA0001.jpg", result.Messages[0].MediaFileName);
    }

    [TestMethod]
    public void Parse_AttachedAudio_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: PTT-20240115-WA0001.opus (file attached)";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageType.Audio, result.Messages[0].Type);
    }

    [TestMethod]
    public void Parse_IosAttachedTag_DetectedCorrectly()
    {
        var chatText = "[07/04/2026, 15:26:18] Alice: <attached: 00000004-PHOTO-2026-04-07-15-26-18.jpg>";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageType.Image, result.Messages[0].Type);
        Assert.AreEqual("00000004-PHOTO-2026-04-07-15-26-18.jpg", result.Messages[0].MediaFileName);
    }

    [TestMethod]
    public void Parse_IosAttachedTagWithDirectionMark_DetectedCorrectly()
    {
        var chatText = "[07/04/2026, 15:26:18] Alice: \u200E<attached: IMG-1234.jpg>";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        Assert.AreEqual(MessageType.Image, result.Messages[0].Type);
        Assert.AreEqual("IMG-1234.jpg", result.Messages[0].MediaFileName);
    }

    [TestMethod]
    public void Parse_MultilineMessage_JoinsLines()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Line one\nLine two\nLine three";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(1, result.Messages.Count);
        StringAssert.Contains(result.Messages[0].Content, "Line one");
        StringAssert.Contains(result.Messages[0].Content, "Line two");
        StringAssert.Contains(result.Messages[0].Content, "Line three");
    }

    [TestMethod]
    public void Parse_CollectsParticipants()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Hello!\n1/15/24, 9:31 AM - Bob: Hi!\n1/15/24, 9:32 AM - Alice: How are you?";
        var result = _parser.Parse(chatText);

        Assert.AreEqual(2, result.Participants.Count);
        CollectionAssert.Contains(result.Participants, "Alice");
        CollectionAssert.Contains(result.Participants, "Bob");
    }

    [TestMethod]
    public void Parse_EmptyChatText_ReturnsEmptyConversation()
    {
        var result = _parser.Parse("");
        Assert.AreEqual(0, result.Messages.Count);
    }

    [TestMethod]
    public async Task ParseZipAsync_InvalidZip_ThrowsInvalidDataException()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream(bytes);
        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => _parser.ParseZipAsync(stream));
    }

    [TestMethod]
    public async Task ParseZipAsync_ZipWithoutChatFile_ThrowsInvalidOperationException()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("photo.jpg");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0xFF, 0xD8 });
        }
        ms.Position = 0;
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => _parser.ParseZipAsync(ms));
    }

    [TestMethod]
    public async Task ParseZipAsync_ZipWithChatFile_ParsesSuccessfully()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Hello!\n1/15/24, 9:31 AM - Bob: Hi!";
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("_chat.txt");
            using var entryStream = entry.Open();
            using var writer = new System.IO.StreamWriter(entryStream);
            await writer.WriteAsync(chatText);
        }
        ms.Position = 0;
        var result = await _parser.ParseZipAsync(ms);

        Assert.AreEqual(2, result.Messages.Count);
        CollectionAssert.Contains(result.Participants, "Alice");
        CollectionAssert.Contains(result.Participants, "Bob");
    }
}

