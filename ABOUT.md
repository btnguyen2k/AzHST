# AzHST

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
- **Continuing projects:** Reopen local projects and refine the same visual
  answer through follow-up requests that retain the Copilot conversation.
- **Presentation-ready output:** Export the visualization as an editable
  PowerPoint deck for reviews, workshops, and stakeholder discussions.
- **Traceable recommendations:** Follow links to the official Microsoft and
  Azure documentation used as sources.
- **Local ownership:** Keep project history, generated HTML pages, and
  PowerPoint files on your computer.

## Quick user guide

### Quick start application

Launch an extracted build normally to keep settings and project history in
your operating system's per-user application data directory:

```text
# Windows
.\AzHST.Desktop.exe

# macOS or Linux
./AzHST.Desktop
```

Add `--portable` to keep AzHST-owned writable data beside the executable:

```text
# Windows
.\AzHST.Desktop.exe --portable

# macOS or Linux
./AzHST.Desktop --portable
```

When running from source, place application arguments after `--`:

```text
dotnet run --project src/AzHST.Desktop/AzHST.Desktop.csproj --configuration Release -- --portable
```

| AzHST argument | Behavior |
|---|---|
| `--portable` | Stores `settings.json`, `projects.db`, Copilot SDK data, and the sample-query database under `<application-directory>\data`; the default generated-output directory becomes `<application-directory>\generated`. |

No other AzHST-specific command-line arguments are currently supported.
Portable mode requires a writable extracted application directory and uses a
separate data store; it does not automatically import data from a normal
launch. GitHub CLI authentication remains managed by `gh` for the current
operating-system user.

1. **Sign in to GitHub.** Select **GitHub sign-in instructions**, run the
   displayed command in your terminal, complete the browser flow, and then
   select **I've signed in**:

   ```text
   gh auth login --hostname github.com --git-protocol https --web
   ```

2. **Start a project.** Choose a suggested question or enter your own request,
   such as:
   - How does Azure Application Gateway work?
   - Compare Azure Front Door and Azure Application Gateway.
   - Propose a resilient Azure architecture for a multi-region web application.
   Select the refresh button beside **TRY AN EXAMPLE** to load another set of
   suggestions from the local sample-query catalog.

3. **Create the visualization.** Select **Create project** to generate an
   interactive explanation with diagrams, guidance, and source links.

4. **Continue the conversation.** Enter a follow-up request and select
   **Refine visualization**. AzHST updates revision `000` while preserving the
   previous valid result if generation fails. The sidebar keeps the three most
   recently updated projects and pins the active project when needed. Select
   **Browse projects** to search and reopen the complete project history.

5. **Explore or share the result.** Review it inside AzHST or select
   **Open in browser**.

6. **Create a presentation.** Select **Build PowerPoint**, then
   **Open PowerPoint** to review the editable deck.

7. **Adjust preferences.** Open **Settings** to choose the Copilot model,
   visualization theme, PowerPoint theme, output directory, and browser
   behavior.

Project history is stored locally. Generated pages and presentations remain
in the configured output directory.

## Azure icon usage

AzHST includes official Azure architecture icons from the
[Azure Architecture Center](https://learn.microsoft.com/azure/architecture/icons/).
The icons are used only in generated architecture diagrams, technical
explanations, training material, and documentation.

AzHST preserves the original icon artwork and follows Microsoft's published
usage guidance: icons are not cropped, flipped, rotated, recolored, distorted,
or used to represent AzHST or non-Microsoft products. See the
[Azure icon source and usage summary](https://github.com/btnguyen2k/AzHST/blob/main/resources/azure-icons/README.md).

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

## License

AzHST is available under the
[MIT License](https://github.com/btnguyen2k/AzHST/blob/main/LICENSE.md).
