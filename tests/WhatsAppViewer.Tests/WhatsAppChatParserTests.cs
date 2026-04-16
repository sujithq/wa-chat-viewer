using WhatsAppViewer.Models;
using WhatsAppViewer.Services;

namespace WhatsAppViewer.Tests;

public class WhatsAppChatParserTests
{
    private readonly WhatsAppChatParser _parser = new();

    [Fact]
    public void Parse_AndroidFormat_ParsesMessages()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Hello!\n1/15/24, 9:31 AM - Bob: Hi there!";
        var result = _parser.Parse(chatText);

        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("Alice", result.Messages[0].Sender);
        Assert.Equal("Hello!", result.Messages[0].Content);
        Assert.Equal("Bob", result.Messages[1].Sender);
        Assert.Equal("Hi there!", result.Messages[1].Content);
    }

    [Fact]
    public void Parse_iOSFormat_ParsesMessages()
    {
        var chatText = "[15/01/2024, 09:30:00] Alice: Hello!\n[15/01/2024, 09:31:00] Bob: Hi there!";
        var result = _parser.Parse(chatText);

        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("Alice", result.Messages[0].Sender);
        Assert.Equal("Hello!", result.Messages[0].Content);
    }

    [Fact]
    public void Parse_SystemMessage_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:00 AM - Messages and calls are end-to-end encrypted.\n1/15/24, 9:01 AM - Alice: Hey!";
        var result = _parser.Parse(chatText);

        Assert.Equal(2, result.Messages.Count);
        Assert.True(result.Messages[0].IsSystem);
        Assert.False(result.Messages[1].IsSystem);
    }

    [Fact]
    public void Parse_MediaOmitted_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: <Media omitted>";
        var result = _parser.Parse(chatText);

        Assert.Single(result.Messages);
        Assert.Equal(MessageType.MediaOmitted, result.Messages[0].Type);
    }

    [Fact]
    public void Parse_AttachedImage_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: IMG-20240115-WA0001.jpg (file attached)";
        var result = _parser.Parse(chatText);

        Assert.Single(result.Messages);
        Assert.Equal(MessageType.Image, result.Messages[0].Type);
        Assert.Equal("IMG-20240115-WA0001.jpg", result.Messages[0].MediaFileName);
    }

    [Fact]
    public void Parse_AttachedAudio_DetectedCorrectly()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: PTT-20240115-WA0001.opus (file attached)";
        var result = _parser.Parse(chatText);

        Assert.Single(result.Messages);
        Assert.Equal(MessageType.Audio, result.Messages[0].Type);
    }

    [Fact]
    public void Parse_MultilineMessage_JoinsLines()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Line one\nLine two\nLine three";
        var result = _parser.Parse(chatText);

        Assert.Single(result.Messages);
        Assert.Contains("Line one", result.Messages[0].Content);
        Assert.Contains("Line two", result.Messages[0].Content);
        Assert.Contains("Line three", result.Messages[0].Content);
    }

    [Fact]
    public void Parse_CollectsParticipants()
    {
        var chatText = "1/15/24, 9:30 AM - Alice: Hello!\n1/15/24, 9:31 AM - Bob: Hi!\n1/15/24, 9:32 AM - Alice: How are you?";
        var result = _parser.Parse(chatText);

        Assert.Equal(2, result.Participants.Count);
        Assert.Contains("Alice", result.Participants);
        Assert.Contains("Bob", result.Participants);
    }

    [Fact]
    public void Parse_EmptyChatText_ReturnsEmptyConversation()
    {
        var result = _parser.Parse("");
        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task ParseZipAsync_InvalidZip_ThrowsException()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02, 0x03 };
        using var stream = new MemoryStream(bytes);
        await Assert.ThrowsAnyAsync<Exception>(() => _parser.ParseZipAsync(stream));
    }

    [Fact]
    public async Task ParseZipAsync_ZipWithoutChatFile_ThrowsException()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("photo.jpg");
            using var entryStream = entry.Open();
            entryStream.Write(new byte[] { 0xFF, 0xD8 });
        }
        ms.Position = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => _parser.ParseZipAsync(ms));
    }

    [Fact]
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

        Assert.Equal(2, result.Messages.Count);
        Assert.Contains("Alice", result.Participants);
        Assert.Contains("Bob", result.Participants);
    }
}
