[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Actions Status](https://github.com/btnguyen2k/AzHST/workflows/CI/badge.svg)](https://github.com/btnguyen2k/AzHST/actions)
[![Release](https://img.shields.io/github/release/btnguyen2k/AzHST.svg?style=flat-square)](RELEASE-NOTES.md)

**Azure - How stuff works (AzHST)** turns questions about Azure and Microsoft cloud services into clear,
interactive visual explanations. It helps people understand services, compare
options, and communicate cloud designs without working through long AI
responses.

## Highlights

- **Visual answers:** Explore service behavior, request flows, dependencies,
  and architecture decisions through interactive visualizations.
- **Architecture guidance:** Describe business and technical requirements and
  receive a visual Azure architecture proposal with operational considerations.
- **Service comparisons:** Compare Azure services, capabilities, trade-offs,
  and suitable use cases side by side.
- **Presentation-ready output:** Export the visualization as an editable
  PowerPoint deck for reviews, workshops, and stakeholder discussions.
- **Traceable recommendations:** Follow links to the official Microsoft and
  Azure documentation used as sources.
- **Local ownership:** Keep generated HTML pages and PowerPoint files on your
  computer and open them in AzHST or your preferred applications.

## Quick installation

### Windows: download a ready-to-run build

1. Install [GitHub CLI](https://cli.github.com/).
2. Ensure your GitHub account has access to GitHub Copilot.
3. Open the repository's
   [Releases](https://github.com/btnguyen2k/AzHST/releases) page.
4. Download the latest `AzHST-<version>-win-x64.zip` archive.
5. Extract the complete archive to a folder. Keep the executable and its
   accompanying files together.
6. Run `AzHST.Desktop.exe`.

The Windows package is self-contained and does not require the .NET SDK.
Microsoft Edge WebView2 is also required and is already included with Windows
11 and most supported Windows 10 installations.

### Build and run from source

#### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Git](https://git-scm.com/)
- [GitHub CLI](https://cli.github.com/)
- A GitHub account with access to GitHub Copilot

Windows, macOS, and Linux users can build and run AzHST directly from the
repository. Windows uses Microsoft Edge WebView2, and macOS uses the built-in
WKWebView runtime. On Debian or Ubuntu Linux, install the native WebView
dependencies:

```bash
sudo apt install libgtk-3-0 libwebkit2gtk-4.1-0 libsoup-3.0-0
```

Clone, build, and launch AzHST:

```text
git clone https://github.com/btnguyen2k/AzHST.git
cd AzHST

dotnet restore AzHST.slnx
dotnet build AzHST.slnx --configuration Release
dotnet run --project src/AzHST.Desktop/AzHST.Desktop.csproj --configuration Release
```

## Quick user guide

1. **Sign in to GitHub.** Select **GitHub sign-in instructions**, run the
   displayed command in your terminal, complete the browser flow, and then
   select **I've signed in**:

   ```text
   gh auth login --hostname github.com --git-protocol https --web
   ```

2. **Ask a question.** Choose a suggested question or enter your own request,
   such as:
   - How does Azure Application Gateway work?
   - Compare Azure Front Door and Azure Application Gateway.
   - Propose a resilient Azure architecture for a multi-region web application.

3. **Generate the visualization.** Select **Generate visualization** to create
   an interactive explanation with diagrams, guidance, and source links.

4. **Explore or share the result.** Review it inside AzHST or select
   **Open in browser**.

5. **Create a presentation.** Select **Build PowerPoint**, then
   **Open PowerPoint** to review the editable deck.

6. **Adjust preferences.** Open **Settings** to choose the Copilot model,
   visualization theme, PowerPoint theme, output directory, and browser
   behavior.

Generated pages and presentations are stored locally in the configured output
directory.

## Azure icon usage

AzHST includes official Azure architecture icons from the
[Azure Architecture Center](https://learn.microsoft.com/azure/architecture/icons/).
The icons are used only in generated architecture diagrams, technical
explanations, training material, and documentation.

AzHST preserves the original icon artwork and follows Microsoft's published
usage guidance: icons are not cropped, flipped, rotated, recolored, distorted,
or used to represent AzHST or non-Microsoft products. See
[`resources/azure-icons/README.md`](resources/azure-icons/README.md) for the
source and usage summary.

## Disclaimer

AzHST is an independent open-source project. It is not affiliated with,
endorsed by, sponsored by, or officially connected to Microsoft, Microsoft
Azure, GitHub, or GitHub Copilot. Product names, service names, logos, icons,
and trademarks belong to their respective owners.

AzHST uses AI to generate technical explanations and architecture guidance.
Generated content may be incomplete, inaccurate, or outdated. Always validate
architecture decisions, security controls, pricing, quotas, regional
availability, and service limits against current official documentation before
using the guidance in production.

## Reporting bugs

Report defects through
[GitHub Issues](https://github.com/btnguyen2k/AzHST/issues/new). Include:

- the AzHST version and operating system
- clear steps to reproduce the problem
- the expected and actual behavior
- the complete visible error message
- screenshots or relevant logs when available

Remove credentials, tokens, confidential queries, and sensitive generated
content before posting.

## Contributing

Contributions are welcome:

1. Open an issue before starting a significant change so the approach can be
   discussed.
2. Fork the repository and create a focused branch.
3. Follow the existing architecture and coding conventions described in
   [`docs/architecture.md`](docs/architecture.md) and [`.dev.md`](.dev.md).
4. Build and validate the change:

   ```bash
   dotnet build AzHST.slnx --configuration Release
   dotnet test AzHST.slnx --configuration Release
   dotnet format AzHST.slnx --verify-no-changes
   ```

5. Submit a pull request that explains the problem, the solution, and any
   user-visible behavior changes.

## License

AzHST is available under the [MIT License](LICENSE.md).
