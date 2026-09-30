# AzHST

**Azure - How stuff works**

AzHST is a cross-platform Avalonia desktop application that turns questions about Azure and Microsoft services into visual, interactive HTML explanations. GitHub Copilot generates a self-contained page, which AzHST stores locally and displays in an embedded native WebView or opens in the default browser.

This repository contains the first functional vertical slice. Authentication, configuration, Copilot generation, HTML validation, local persistence, embedded preview, and external-browser launch are implemented. Conversation history, model discovery, export, and richer follow-up workflows remain future work.

## Features

- Avalonia 12 desktop UI targeting .NET 10
- GitHub CLI sign-in flow and authentication status
- GitHub Copilot SDK integration with no agent tools or host access enabled
- Visual prompts for service explanations, comparisons, integrations, and architecture proposals
- Self-contained HTML generation with inline CSS, JavaScript, and SVG
- Content Security Policy injection and external-resource rejection
- Native WebView preview on Windows, macOS, and Linux
- Automatic default-browser fallback and configurable browser launch
- JSON settings stored in the user's local application data directory

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [GitHub CLI](https://cli.github.com/)
- A GitHub account with access to GitHub Copilot

The .NET Copilot SDK bundles its matching Copilot CLI runtime. A separate Copilot CLI installation is not required.

Native WebView prerequisites:

| Platform | Requirement |
|---|---|
| Windows | Microsoft Edge WebView2; included with Windows 11 and most supported Windows 10 installations |
| macOS | WKWebView; included with macOS |
| Linux | GTK 3, WebKitGTK 4.1, and libsoup 3; WPE WebKit is optional |

For Debian or Ubuntu Linux:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

## Build and run

```powershell
dotnet restore AzHST.slnx
dotnet build AzHST.slnx
dotnet test AzHST.slnx
dotnet run --project src\AzHST.Desktop\AzHST.Desktop.csproj
```

On first use, select **GitHub sign-in instructions**. AzHST shows the command to run in your own terminal:

```text
gh auth login --hostname github.com --git-protocol https --web
```

Finish the visible CLI and browser flow, return to AzHST, and select **I've signed in**. AzHST only performs a non-interactive account check after confirmation.

## Debug authentication overrides

Debug builds recognize two presence-based environment variables for testing authentication UI:

| Variable | Debug behavior |
|---|---|
| `TEST_NO_GH_SIGNIN` | Skips the startup GitHub account check and starts in the signed-out state. The manual sign-in dialog and its post-confirmation status check still work normally. |
| `TEST_NO_GH_BIN` | Simulates GitHub CLI being unavailable for every account check without starting `gh`. |

For example:

```powershell
$env:TEST_NO_GH_SIGNIN = "1"
dotnet run --project src\AzHST.Desktop\AzHST.Desktop.csproj --configuration Debug
```

Only presence matters; the variable's value is ignored. `TEST_NO_GH_BIN` takes precedence if both variables are present. Release builds ignore both variables.

## Project structure

```text
src\
  AzHST.Application\       Use cases, contracts, models, and HTML safety policy
  AzHST.Infrastructure\    Copilot SDK, GitHub CLI, filesystem, settings, and browser adapters
  AzHST.Desktop\           Avalonia views, view models, UI services, and composition root
tests\
  AzHST.Application.Tests\ Core orchestration and generated-document policy tests
docs\
  architecture.md          Design, data flow, trust boundaries, and roadmap
```

Dependencies point inward: Desktop and Infrastructure depend on Application; Application has no UI, SDK, or operating-system dependencies.

## Local data

AzHST uses the operating system's local application data folder:

```text
AzHST\
  settings.json
  generated\
  copilot\
```

The generated output directory can be changed in Settings. Authentication tokens remain managed by GitHub CLI or the bundled Copilot runtime and are never written to `settings.json`.

## Security model

Model output is untrusted. Before saving a page, AzHST requires a complete HTML document, limits its size, rejects external `src` and `href` references and remote CSS URLs, removes `<base>` and model-provided CSP elements, and injects a restrictive Content Security Policy. The desktop host also blocks top-level navigation and new-window requests. Inline CSS, JavaScript, SVG, and data images remain available for interactive visualizations.

Generated technical guidance can still be incomplete or outdated. Validate architecture, pricing, quotas, regional availability, security controls, and service limits against current Microsoft documentation before production use.

See [docs\architecture.md](docs/architecture.md) for the full design.
