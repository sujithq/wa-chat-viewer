# Chat Export Viewer

Chat Export Viewer is a client-side Blazor WebAssembly app that opens exported WhatsApp ZIP files and renders conversations in a familiar chat interface.

The app runs fully in the browser. Chat parsing, media resolution, and rendering all happen locally on the user device.

## What This Project Is

- A static web app built with .NET 10 and Blazor WebAssembly
- A parser for WhatsApp Android and iOS export formats
- A conversation viewer with date separators, message bubbles, and media support
- A repo with unit tests for parser behavior and Playwright tests for core UI checks

## Why Contributors Should Care

- The parser has to handle many real-world export variations
- Media handling and rendering quality are important for user trust
- The app is privacy-oriented and must remain fully client-side
- Small UI decisions have a big impact on readability and authenticity

## Tech Stack

- Framework: Blazor WebAssembly
- Runtime target: net10.0
- Main package: Microsoft.AspNetCore.Components.WebAssembly 10.0.6
- Unit tests: MSTest 3.7.3 with Microsoft Testing Platform runner
- E2E tests: Playwright for .NET with MSTest integration
- Deployment target: GitHub Pages via GitHub Actions

## Repository Structure

- src/WhatsAppViewer
  - Pages/Home.razor: main upload and chat rendering UI
  - Services/WhatsAppChatParser.cs: chat and media parsing logic
  - Models/ChatConversation.cs and Models/ChatMessage.cs: core data model
  - wwwroot/css/app.css: app styling
- tests/WhatsAppViewer.Tests
  - Parser-focused unit tests
- tests/WhatsAppViewer.Playwright.Tests
  - Browser-level UI smoke tests
- .github/workflows/deploy.yml
  - GitHub Pages publish pipeline
- .github/workflows/copilot-setup-steps.yml
  - Pre-restore and Playwright browser setup workflow

## Quick Start

Prerequisites:

- .NET SDK 10
- PowerShell (for Playwright install scripts)

From the repository root:

1. Restore dependencies

	dotnet restore src/WhatsAppViewer/WhatsAppViewer.csproj
	dotnet restore tests/WhatsAppViewer.Tests/WhatsAppViewer.Tests.csproj
	dotnet restore tests/WhatsAppViewer.Playwright.Tests/WhatsAppViewer.Playwright.Tests.csproj

2. Build the app

	dotnet build src/WhatsAppViewer

3. Run locally

	dotnet run --project src/WhatsAppViewer --urls http://localhost:5000

Then open http://localhost:5000 in your browser.

## Testing

### Unit Tests

Run parser tests:

dotnet test tests/WhatsAppViewer.Tests

These tests cover:

- Android and iOS message parsing
- System messages and multiline messages
- Media detection and attachment formats
- ZIP parsing error handling

### Playwright E2E Tests

1. Build Playwright test project

	dotnet build tests/WhatsAppViewer.Playwright.Tests

2. Install Chromium (first time)

	pwsh tests/WhatsAppViewer.Playwright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium

3. Start app

	dotnet run --project src/WhatsAppViewer --urls http://localhost:5000

4. Run E2E tests (new terminal)

	set PLAYWRIGHT_TEST_BASE_URL=http://localhost:5000
	dotnet test tests/WhatsAppViewer.Playwright.Tests --settings tests/WhatsAppViewer.Playwright.Tests/playwright.runsettings

## Parsing Notes for Contributors

The parser currently supports multiple timestamp and message formats and can detect:

- Regular text messages
- System messages
- Media omitted markers
- File attachments in both styles:
  - filename (file attached)
  - <attached: filename>

ZIP safety limits are enforced during extraction to keep browser memory usage bounded:

- Chat text file limit: 25 MB
- Single media file limit: 150 MB
- Total extracted ZIP content limit: 768 MB
- ZIP media entry limit: 1000 files

Only recognized chat log names are parsed as the conversation text file:

- _chat.txt
- chat.txt
- files starting with "WhatsApp Chat with"

When changing parser behavior:

- Add or update unit tests in tests/WhatsAppViewer.Tests/WhatsAppChatParserTests.cs
- Prefer narrow, format-specific regex changes
- Preserve backward compatibility for already supported exports

## UI Notes for Contributors

- Main chat rendering is in src/WhatsAppViewer/Pages/Home.razor
- Styling is centralized in src/WhatsAppViewer/wwwroot/css/app.css
- Message side positioning is currently turn-based by speaker changes
- Inline media presentation aims to resemble WhatsApp while remaining lightweight

When changing UI behavior:

- Validate on desktop and mobile widths
- Check accessibility for icon-only controls (aria-label)
- Keep visual changes scoped and intentional

## Contributing Workflow

1. Create a feature branch from main
2. Make focused changes
3. Run build and relevant tests locally
4. Update docs and tests for behavioral changes
5. Open a pull request with a clear summary and validation steps

Recommended pre-PR checks:

- dotnet build src/WhatsAppViewer
- dotnet test tests/WhatsAppViewer.Tests
- dotnet test tests/WhatsAppViewer.Playwright.Tests (when UI behavior changed)

## Deployment

Deployment is automated on push to main using .github/workflows/deploy.yml.

The workflow:

- Runs unit tests and Playwright smoke tests first
- Publishes Blazor assets
- Rewrites the published base href for GitHub Pages hosting:
	- Uses / when a CNAME file is present for custom-domain deployments
	- Uses /{repo-name}/ for standard GitHub project pages deployments
- Uploads artifact and deploys to GitHub Pages

## Good First Contribution Areas

- Add parser coverage for additional export edge cases
- Improve media fallback behavior for unsupported formats
- Expand Playwright coverage beyond upload screen smoke tests
- Improve keyboard accessibility and focus states
- Performance optimizations for large chat exports

## License

See LICENSE in the repository root.