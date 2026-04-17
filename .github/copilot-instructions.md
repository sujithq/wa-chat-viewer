# GitHub Copilot Custom Instructions

## Project overview

This repository contains **WhatsApp Chat Viewer**, a client-side Blazor WebAssembly application that parses WhatsApp-exported `.zip` files and renders the conversation with a WhatsApp-like UI. No server is required — all parsing runs in-browser via WASM.

## Technology stack

- **Framework**: Blazor WebAssembly (`.NET 10`, `net10.0`)
- **Packages**: `Microsoft.AspNetCore.Components.WebAssembly` 10.0.6
- **Tests**: Both test projects use **MSTest 3.7.3** with the **Microsoft Testing Platform** (`EnableMSTestRunner=true`)
  - Unit tests: `MSTest` 3.7.3 (unified package) with `FrameworkReference Microsoft.AspNetCore.App`
  - E2E tests: `Microsoft.Playwright.MSTest` 1.59.0 + `MSTest.TestAdapter`/`MSTest.TestFramework` 3.7.3 + `Microsoft.NET.Test.Sdk` 18.4.0
- **Hosting**: GitHub Pages (deployed via `.github/workflows/deploy.yml`)
- **Language**: C# with nullable reference types and implicit usings enabled

## Repository structure

```
src/WhatsAppViewer/                     # Blazor WASM app
  Models/                               # ChatConversation.cs, ChatMessage.cs
  Services/                             # WhatsAppChatParser.cs
  Pages/                                # Home.razor (main UI)
  Layout/                               # MainLayout.razor, NavMenu.razor
  wwwroot/                              # Static assets (css, icons, etc.)
tests/WhatsAppViewer.Tests/             # MSTest unit tests (parser logic) — Microsoft Testing Platform
tests/WhatsAppViewer.Playwright.Tests/  # Playwright E2E tests (UI, MSTest) — Microsoft Testing Platform
.github/workflows/
  deploy.yml                            # GitHub Pages deployment
  copilot-setup-steps.yml               # Pre-installs .NET 10 + restores packages before firewall
```

## Build and test commands

```bash
# Build the app
dotnet build src/WhatsAppViewer

# Run unit tests (MSTest / Microsoft Testing Platform)
dotnet test tests/WhatsAppViewer.Tests

# Run E2E tests (Playwright / MSTest / Microsoft Testing Platform)
# 1. Start the dev server first:
dotnet run --project src/WhatsAppViewer --urls http://localhost:5000 &
# 2. Install Playwright browsers (first time only):
pwsh tests/WhatsAppViewer.Playwright.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
# 3. Run the tests:
PLAYWRIGHT_TEST_BASE_URL=http://localhost:5000 \
  dotnet test tests/WhatsAppViewer.Playwright.Tests \
  --settings tests/WhatsAppViewer.Playwright.Tests/playwright.runsettings

# Restore dependencies
dotnet restore src/WhatsAppViewer/WhatsAppViewer.csproj
dotnet restore tests/WhatsAppViewer.Tests/WhatsAppViewer.Tests.csproj
dotnet restore tests/WhatsAppViewer.Playwright.Tests/WhatsAppViewer.Playwright.Tests.csproj
```

## Coding conventions

- **Nullable reference types** are enabled — always annotate types correctly (`string?` vs `string`).
- **Implicit usings** are enabled — do not add redundant `using` directives for common namespaces.
- Use `async`/`await` throughout; avoid `.Result` or `.Wait()` on tasks.
- Razor components use PascalCase filenames and `@code { }` blocks at the bottom of `.razor` files.
- Service classes are registered in `Program.cs` via `builder.Services`.
- Use `MarkupString` for rendering pre-built HTML in Razor; always HTML-escape user content before injecting it.
- Use deterministic (stable) hashing when deriving UI properties (e.g., colors) from string values — do **not** use `string.GetHashCode()` as it is non-deterministic across runs in .NET.

## Parser details

- `WhatsAppChatParser` handles both Android (`MM/DD/YY, HH:MM - Sender: msg`) and iOS (`[DD/MM/YYYY, HH:MM:SS] Sender: msg`) export formats.
- ZIP parsing prefers well-known chat log filenames (`_chat.txt`, `chat.txt`, names starting with `WhatsApp Chat with`); it does **not** blindly treat any `.txt` file as the chat log.
- ZIP entry dictionary keys use `entry.FullName` (not `entry.Name`) to avoid collisions between files in different subdirectories.
- Decompressed content must be size-checked to prevent ZIP-bomb-style OOM crashes in WASM.

## UI / UX

- WhatsApp dark theme: green header (`#075e54`), dark chat background (`#0d1418`), green sent bubbles, dark received bubbles.
- Date dividers separate messages by day.
- Sender colors are derived from a deterministic hash of the sender name mapped to a fixed palette.
- Images open in a lightbox; audio and video render inline.
- All interactive icon-only buttons must have an `aria-label` attribute for accessibility.
- The lightbox dialog uses `role="dialog"` and `aria-modal="true"`.

## Testing guidelines

### Unit tests (MSTest / Microsoft Testing Platform)
- Place all unit tests in `tests/WhatsAppViewer.Tests/`.
- Test class names follow the `<Class>Tests` convention (e.g., `WhatsAppChatParserTests`).
- Use `[TestClass]` and `[TestMethod]` attributes; use `Assert.AreEqual`, `Assert.IsTrue`, `StringAssert.Contains`, `CollectionAssert.Contains`.
- Use `await Assert.ThrowsExceptionAsync<T>` for async exception assertions — assert the specific exception type (e.g., `InvalidDataException`) not the base `Exception` class.
- Cover both Android and iOS format variants for any parsing change.
- Both test projects use `<EnableMSTestRunner>true</EnableMSTestRunner>` and `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` for MTP support.

### E2E tests (Playwright / MSTest / Microsoft Testing Platform)
- Place all E2E tests in `tests/WhatsAppViewer.Playwright.Tests/`.
- Use `Microsoft.Playwright.MSTest` 1.59.0 with `MSTest.TestAdapter`/`MSTest.TestFramework` 3.7.3 and `Microsoft.NET.Test.Sdk` 18.4.0.
- Test classes inherit from `PageTest` and override `ContextOptions()` to set `BaseURL` from `PLAYWRIGHT_TEST_BASE_URL` (defaults to `http://localhost:5000`).
- Use `playwright.runsettings` to run headless Chromium.
- The `copilot-setup-steps.yml` builds the project and installs browsers with `playwright.ps1 install chromium`.

## GitHub Actions / deployment

- Deployment to GitHub Pages is triggered on push to `main` via `deploy.yml`.
- The `<base href>` is dynamically rewritten to `/{repo-name}/` for correct GitHub Pages URL routing.
- Always use the latest major versions of GitHub Actions:
  - `actions/checkout@v6`
  - `actions/setup-dotnet@v5`
  - `actions/upload-pages-artifact@v5`
  - `actions/deploy-pages@v5`
- The `copilot-setup-steps.yml` pre-installs .NET 10 and restores NuGet packages **before** the agent firewall activates, ensuring all packages are cached for offline use during agent sessions.
