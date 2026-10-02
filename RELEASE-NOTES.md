# AzHST Release Notes

## Unreleased

- Fixed single-file application packages so the RID-specific GitHub Copilot
  runtime remains available beside the executable for visualization generation.
- Completed the .NET 10 CI workflow with cross-platform Release builds, tests,
  formatting verification, semantic-release analysis, and versioned
  self-contained archives plus SHA-256 checksums.
- Added account-aware GitHub Copilot model selection for visualization and
  PowerPoint generation, with Automatic retained as the default.
- Improved PowerPoint fidelity by preserving parent sections, nested headings,
  explanations, and visible callouts from the HTML narrative, and removed
  detached top-accent strips from visual cards.
- Added validated clickable links to the HTML Sources section. PowerPoint now
  keeps the traceability footer only on the title slide and appends a final
  standalone Resources / Sources slide with a compact numbered bibliography.
- Fixed intermittent no-response behavior from **Build PowerPoint** by removing
  the unnecessary authentication gate for manifest-backed builds and showing
  complete SDK/build failures in a persistent selectable error panel.
- Added a tabbed About dialog that displays embedded application information
  and these release notes.
