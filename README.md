# AzHST

**Azure - How stuff works**

AzHST is a cross-platform Avalonia desktop application that turns questions about Azure and Microsoft services into visual, interactive HTML explanations. GitHub Copilot generates a self-contained page, which AzHST stores locally and displays in an embedded native WebView or opens in the default browser.

This repository contains the first functional vertical slice. Authentication, configuration, Copilot generation, HTML validation, local persistence, embedded preview, and external-browser launch are implemented. Conversation history, export, and richer follow-up workflows remain future work.

## Features

- Avalonia 12 desktop UI targeting .NET 10
- GitHub CLI sign-in flow and authentication status
- GitHub Copilot SDK integration with no agent tools or host access enabled
- Automatic Copilot model selection
- Copilot-powered query validation for Azure relevance and visual suitability
- Visual prompts for service explanations, comparisons, integrations, and architecture proposals
- Self-contained HTML generation with inline CSS, JavaScript, SVG, and official Azure service icons
- Visual-first 16:9 PowerPoint generation derived from the secured HTML structure, with native editable cards, diagrams, connectors, and Azure icons
- Validated JSON theme catalog with separate HTML and PowerPoint selections
- Timestamp-and-slug visualization IDs with one directory per generated page
- Content Security Policy injection and external-resource rejection
- Native WebView preview on Windows, macOS, and Linux
- Automatic default-browser fallback and configurable browser launch
- Runtime About and Changelog tabs backed by Markdown embedded in the application assembly
- JSON settings stored in the user's local application data directory
- SQLite-backed sample question catalog with six categories and ten questions per category
- Four randomized home-page suggestions, refreshed whenever the home page opens
- Weekly Copilot regeneration of sample questions with atomic database replacement

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
  AzHST.Infrastructure.Tests\ Filesystem, icon catalog, and PowerPoint package tests
docs\
  architecture.md          Design, data flow, trust boundaries, and roadmap
resources\
  azure-icons\             Official Azure architecture SVG icon catalog
  themes\                  Validated HTML and PowerPoint theme definitions
```

Dependencies point inward: Desktop and Infrastructure depend on Application; Application has no UI, SDK, or operating-system dependencies.

## Generation flow

For each submitted question, AzHST:

1. Uses Copilot structured output to verify that the request is related to Azure or Microsoft cloud services and can produce a meaningful visual explanation.
2. Shows the returned guidance without generating a page when the request is invalid.
3. Creates an ID from the hexadecimal Unix timestamp and a sanitized Copilot-suggested slug.
4. Selects a small query-relevant catalog of approved Azure icon keys.
5. Resolves the selected HTML theme and makes a second Copilot request for a standalone page with clear text sections, a prominent visual stage, stateful controls, purposeful animation, reduced-motion support, and optional official icon placeholders.
6. Replaces approved placeholders with the original SVG bytes encoded as `data:` images and injects the authoritative theme CSS.
7. Secures and saves the page, then navigates the embedded WebView to it.
8. On request, extracts a bounded structural outline from the secured HTML, asks Copilot for a visual-first slide plan that follows the page's section order and terminology, resolves the selected presentation theme, and builds an editable `.pptx` locally with the Open XML SDK.

The default artifact layout is relative to the application's working directory:

```text
generated\
  0199abcdef12-azure-app-gateway\
    index.html
    presentation.pptx
```

The generated root can be changed in Settings.

After a visualization is ready, select **Build PowerPoint**. AzHST creates a
title slide plus 4-9 content slides and stores the deck beside `index.html`.
The first content slide is visual, and at least two thirds of the content
slides are editable diagrams, comparisons, or card grids. Feature grids,
recommendations, trade-offs, and production caveats become separate native
cards instead of dense text boxes. When the HTML presents interactive states
or progressive steps, adjacent slides represent those states; PowerPoint
animation is not generated.
PowerPoint decks use the selected presentation theme. The default is
**Professional Light**, optimized for projection, printing, and document
sharing.
The generated file location is displayed in the application with actions to
copy its path or open its containing folder. Select **Open PowerPoint** to
launch the file in the operating system's default presentation application.
Microsoft PowerPoint is not required to generate the file; PowerPoint,
LibreOffice Impress, or another compatible viewer is required to open it.

## About and release notes

Select the information icon in the main window to open the application
information dialog. The default **About** tab renders this README, while the
**Changelog** tab renders `RELEASE-NOTES.md`.

Both Markdown files are embedded in `AzHST.Desktop.dll` and read at runtime,
so installed builds do not depend on repository files being present. AzHST
converts them to self-contained, CSP-protected HTML for the embedded WebView.
When no compatible WebView runtime is available, the dialog displays the
embedded Markdown as selectable text instead.

## Local application data

Settings and Copilot runtime data use the operating system's local application data folder:

```text
AzHST\
  settings.json
  copilot\
```

Authentication tokens remain managed by GitHub CLI or the bundled Copilot runtime and are never written to `settings.json`.

The sample question catalog is stored separately relative to the application's
working directory:

```text
data\
  azhst.db
```

At startup, AzHST checks the SQLite integrity, application identifier, schema
version, required tables, foreign keys, and generation timestamp. An invalid
or incompatible database is reset and reseeded with ten questions for each of
the six supported visualization categories. The home page chooses four random
categories and one random question from each category.

When the stored question set reaches seven days old and GitHub authentication
is available, Copilot generates a complete replacement set. AzHST validates
all 60 questions before replacing the existing rows in one transaction. A
failed refresh leaves the previous suggestions available for a later retry.

## Output themes

Settings provides separate selectors for HTML visualizations and PowerPoint
presentations. The defaults are **Azure Night** (`azure-night`) for HTML and
**Professional Light** (`professional-light`) for PowerPoint.

Theme definitions are loaded from `resources\themes`, strictly validated, and
applied automatically during generation. They contain `schemaVersion` for the
configuration contract but deliberately have no theme-version property. See
[`.dev.md`](.dev.md) for the complete schema, validation rules, renderer
mappings, and instructions for adding themes.

## Security model

Model output is untrusted. Before saving a page, AzHST requires a complete HTML document and an interactive experience baseline: inline JavaScript, semantic controls, purposeful motion, and a `prefers-reduced-motion` fallback. It also limits document size, rejects external `src` and `href` references and remote CSS URLs, removes `<base>` and model-provided CSP elements, and injects a restrictive Content Security Policy. Azure icon placeholders accept only catalog keys, descriptive alt text, and no model-supplied `src`; AzHST substitutes a validated base64 SVG data URI. The desktop host blocks top-level navigation and new-window requests.

Copilot never generates PowerPoint binary or Open XML markup. It returns a
structured presentation plan based on a bounded structural outline of the
secured HTML. The outline preserves visible headings, text, lists, table rows,
controls, image descriptions, and SVG labels while excluding scripts, styles,
and embedded image bytes. AzHST validates the bounded slide, text, node,
connection, semantic-tone, and icon fields, constructs the package with the
[Microsoft Open XML SDK](https://learn.microsoft.com/office/open-xml/presentation/overview),
embeds approved Azure SVGs, validates the completed package against the Office
schema, and atomically writes `presentation.pptx`. The generated deck contains
no macros, external links, or remote resources.

## Azure icon usage

The bundled SVGs come from the
[Microsoft Azure Architecture Center](https://learn.microsoft.com/azure/architecture/icons/).
Microsoft permits these icons in architectural diagrams, training materials,
and documentation. AzHST uses them only for those purposes inside generated
technical explanations.

Generated pages preserve the original SVG bytes, show the Microsoft product
name near the icon, and instruct Copilot not to crop, flip, rotate, recolor,
distort, or use an icon to represent AzHST or a non-Microsoft product. See
[`resources\azure-icons\README.md`](resources/azure-icons/README.md) for the
source and usage summary.

Generated technical guidance can still be incomplete or outdated. Validate architecture, pricing, quotas, regional availability, security controls, and service limits against current Microsoft documentation before production use.

See [docs\architecture.md](docs/architecture.md) for the full design.
