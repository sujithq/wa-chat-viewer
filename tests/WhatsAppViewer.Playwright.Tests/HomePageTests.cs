using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO.Compression;
using System.Text;

namespace WhatsAppViewer.Playwright.Tests;

/// <summary>
/// End-to-end tests for the WhatsApp Chat Viewer Blazor WASM home page.
/// Before running these tests, start the app with:
///   dotnet run --project src/WhatsAppViewer --urls http://localhost:5000
/// Then set the PLAYWRIGHT_TEST_BASE_URL environment variable if using a
/// different port, or leave it unset to default to http://localhost:5000.
/// </summary>
[TestClass]
public class HomePageTests : PageTest
{
    private static readonly string BaseUrl =
        Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_BASE_URL") ?? "http://localhost:5000";

    public override BrowserNewContextOptions ContextOptions() =>
        new() { BaseURL = BaseUrl };

    private static byte[] CreateZipWithChat(string chatContent)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("_chat.txt");
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(chatContent);
        }

        return ms.ToArray();
    }

    private static byte[] CreateZipWithChatAndMedia(string chatContent, string mediaFileName, byte[] mediaBytes)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var chatEntry = archive.CreateEntry("_chat.txt");
            using (var stream = chatEntry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(chatContent);
            }

            var mediaEntry = archive.CreateEntry(mediaFileName);
            using (var mediaStream = mediaEntry.Open())
            {
                mediaStream.Write(mediaBytes, 0, mediaBytes.Length);
            }
        }

        return ms.ToArray();
    }

    private static byte[] CreateZipWithEntries(params (string Name, byte[] Data)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var entryStream = entry.Open();
                entryStream.Write(data, 0, data.Length);
            }
        }

        return ms.ToArray();
    }

    private async Task UploadChatAsync(string chatContent)
    {
        await Page.GotoAsync("/");

        var file = new FilePayload
        {
            Name = "chat-export.zip",
            MimeType = "application/zip",
            Buffer = CreateZipWithChat(chatContent)
        };

        await Page.SetInputFilesAsync(".wa-file-input", file);
        await Expect(Page.Locator(".wa-chat-area")).ToBeVisibleAsync();
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public async Task PageTitle_IsWhatsAppChatViewer()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync("Chat Export Viewer");
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public async Task Header_ShowsAppTitle()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".wa-title")).ToContainTextAsync("Chat Export Viewer");
    }

    [TestMethod]
    public async Task UploadCard_HeadingIsVisible()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator("h2").First).ToContainTextAsync("Chat Export Viewer");
    }

    [TestMethod]
    public async Task DropZone_IsVisible()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".wa-dropzone")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task ExportInstructions_AreVisible()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".wa-instructions")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task UploadDescription_MentionsPrivacy()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".wa-upload-desc"))
            .ToContainTextAsync("never leaves your device");
    }

    [TestMethod]
    public async Task SearchControls_AppearAfterChatUpload()
    {
        await UploadChatAsync("1/15/24, 9:30 AM - Alice: Hello\n1/15/24, 9:31 AM - Bob: Hi");

        await Expect(Page.Locator(".wa-search")).ToBeVisibleAsync();
        await Expect(Page.Locator(".wa-search-input")).ToBeVisibleAsync();
    }

    [TestMethod]
    public async Task Search_NextPrevious_UpdatesActiveMatch()
    {
        await UploadChatAsync(
            "1/15/24, 9:30 AM - Alice: hello one\n" +
            "1/15/24, 9:31 AM - Bob: hello two\n" +
            "1/15/24, 9:32 AM - Alice: hello three");

        var input = Page.Locator(".wa-search-input");
        await input.FillAsync("hello");

        await Expect(Page.Locator(".wa-search-count")).ToContainTextAsync("1/3");
        await Expect(Page.Locator(".wa-highlight-active")).ToHaveCountAsync(1);

        await Page.Locator("[aria-label='Next match']").ClickAsync();
        await Expect(Page.Locator(".wa-search-count")).ToContainTextAsync("2/3");

        await Page.Locator("[aria-label='Previous match']").ClickAsync();
        await Expect(Page.Locator(".wa-search-count")).ToContainTextAsync("1/3");
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public async Task ClickingImage_OpensLightbox()
    {
        await Page.GotoAsync("/");

        var oneByOnePng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO8M6fUAAAAASUVORK5CYII=");
        var chat = "1/15/24, 9:30 AM - Alice: IMG-20240115-WA0001.jpg (file attached)";

        var file = new FilePayload
        {
            Name = "chat-export.zip",
            MimeType = "application/zip",
            Buffer = CreateZipWithChatAndMedia(chat, "IMG-20240115-WA0001.jpg", oneByOnePng)
        };

        await Page.SetInputFilesAsync(".wa-file-input", file);
        await Expect(Page.Locator(".wa-media-img img")).ToBeVisibleAsync();

        await Page.Locator(".wa-media-img img").First.ClickAsync();
        await Expect(Page.Locator(".wa-lightbox")).ToBeVisibleAsync();
        await Expect(Page.Locator(".wa-lightbox img")).ToBeVisibleAsync();

        await Page.Locator("[aria-label='Close image preview']").ClickAsync();
        await Expect(Page.Locator(".wa-lightbox")).ToHaveCountAsync(0);
    }

    [TestMethod]
    [TestCategory("Smoke")]
    public async Task UploadWithUnsupportedChatFile_ShowsErrorToast()
    {
        await Page.GotoAsync("/");

        var invalidZip = CreateZipWithEntries(
            ("notes.txt", Encoding.UTF8.GetBytes("This is not a WhatsApp export")));

        var file = new FilePayload
        {
            Name = "chat-export.zip",
            MimeType = "application/zip",
            Buffer = invalidZip
        };

        await Page.SetInputFilesAsync(".wa-file-input", file);
        await Expect(Page.Locator(".wa-error-toast")).ToContainTextAsync("No supported chat text file found");
    }
}
