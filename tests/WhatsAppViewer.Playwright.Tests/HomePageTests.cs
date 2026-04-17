using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

    [TestMethod]
    public async Task PageTitle_IsWhatsAppChatViewer()
    {
        await Page.GotoAsync("/");
        await Expect(Page).ToHaveTitleAsync("WhatsApp Chat Viewer");
    }

    [TestMethod]
    public async Task Header_ShowsAppTitle()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator(".wa-title")).ToContainTextAsync("WhatsApp Chat Viewer");
    }

    [TestMethod]
    public async Task UploadCard_HeadingIsVisible()
    {
        await Page.GotoAsync("/");
        await Expect(Page.Locator("h2").First).ToContainTextAsync("WhatsApp Chat Viewer");
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
}
