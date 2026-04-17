## Summary

Describe what this PR changes and why.

## Type Of Change

- [ ] Bug fix
- [ ] Feature
- [ ] Refactor
- [ ] Tests only
- [ ] Documentation only

## What Changed

- 
- 
- 

## Risk And Impact

- Affected area(s): parser, UI, media handling, deployment, tests
- Potential regressions:
- Mitigation:

## Validation

Record what you ran and the result.

- [ ] dotnet build src/WhatsAppViewer
- [ ] dotnet test tests/WhatsAppViewer.Tests
- [ ] dotnet test tests/WhatsAppViewer.Playwright.Tests (required when UI behavior changes)

## Parser Change Checklist (required when touching parser logic)

- [ ] Updated parser tests in tests/WhatsAppViewer.Tests/WhatsAppChatParserTests.cs
- [ ] Covered both Android and iOS formats where applicable
- [ ] Covered edge cases for attachments/media markers where applicable
- [ ] Verified no regressions for existing supported formats

## UI Change Checklist (required when touching chat rendering or styling)

- [ ] Added before/after screenshots
- [ ] Verified desktop and mobile layout behavior
- [ ] Verified accessibility for icon-only actions (aria-label)
- [ ] Ran Playwright tests or documented why not

## Screenshots (required for UI changes)

Add before/after screenshots.

## Documentation

- [ ] README updated when contributor workflow or behavior changed
- [ ] Comments/docs updated for non-obvious logic

## Final Checklist

- [ ] Scope is focused and related to one change
- [ ] Tests were added or updated for behavioral changes
- [ ] No secrets or sensitive data were added
