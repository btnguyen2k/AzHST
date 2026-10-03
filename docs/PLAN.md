# AzHST Development Plan

## Product direction

The next development should make AzHST an iterative architecture workspace
rather than a one-shot generator. Users should be able to reopen an existing
visualization, request targeted changes, and preserve earlier versions without
starting over.

## Priorities

| Priority | Initiative | Intended business impact |
| --- | --- | --- |
| 1 | **Projects, history, and follow-up refinement** | Let users reopen previous questions, preserve generated files, and iteratively improve an architecture or presentation. |
| 2 | **Guided architecture templates** | Provide structured inputs for comparisons, migrations, landing zones, resiliency, and integrations so users can produce consistent results with less prompt-writing experience. |
| 3 | **Trust and architecture review** | Show assumptions, unresolved questions, source freshness, and checks against Azure Well-Architected pillars such as security, reliability, cost, and operations. |
| 4 | **Generation controls and reliability** | Allow cancellation, retry, resume, and model fallback while preserving partial results and providing useful diagnostics when generation fails. |
| 5 | **Output customization** | Let users choose the audience, technical depth, slide count, included sections, and diagram complexity before generating HTML or PowerPoint. |
| 6 | **Easier installation and updates** | Add a Windows installer and update notification, followed by signed or notarized macOS packages and packaged Linux releases. |
| 7 | **Additional sharing formats** | Export diagrams as SVG or PNG and documents as PDF alongside the existing HTML and PowerPoint outputs. |
| 8 | **Privacy assistance** | Detect likely credentials, subscription IDs, internal hostnames, and confidential information before a prompt is sent to GitHub Copilot. |

## Focused next-development scope

1. Add a local project and generation-history browser.
2. Support follow-up prompts that revise an existing visualization.
3. Preserve generated versions so users can compare or restore previous
   results.
4. Display assumptions, source dates, and unanswered requirements.
5. Add cancellation, retry, and clearer generation progress.
