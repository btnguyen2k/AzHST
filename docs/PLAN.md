# AzHST Development Plan

## Product direction

The next development should make AzHST an iterative architecture workspace
rather than a one-shot generator. Users should be able to reopen an existing
project and continue its Copilot conversation with targeted follow-up requests
without starting over.

## Priorities

| Priority | Initiative | Intended business impact |
| --- | --- | --- |
| 1 | **Projects, history, and follow-up refinement** | Let users reopen previous questions and continue a persistent Copilot conversation that improves the current architecture or presentation. |
| 2 | **Guided architecture templates** | Provide structured inputs for comparisons, migrations, landing zones, resiliency, and integrations so users can produce consistent results with less prompt-writing experience. |
| 3 | **Trust and architecture review** | Show assumptions, unresolved questions, source freshness, and checks against Azure Well-Architected pillars such as security, reliability, cost, and operations. |
| 4 | **Generation controls and reliability** | Allow cancellation, retry, resume, and model fallback while preserving partial results and providing useful diagnostics when generation fails. |
| 5 | **Output customization** | Let users choose the audience, technical depth, slide count, included sections, and diagram complexity before generating HTML or PowerPoint. |
| 6 | **Easier installation and updates** | Add a Windows installer and update notification, followed by signed or notarized macOS packages and packaged Linux releases. |
| 7 | **Additional sharing formats** | Export diagrams as SVG or PNG and documents as PDF alongside the existing HTML and PowerPoint outputs. |
| 8 | **Privacy assistance** | Detect likely credentials, subscription IDs, internal hostnames, and confidential information before a prompt is sent to GitHub Copilot. |

## Focused next-development scope

1. Add a local project and generation-history browser.
2. Support follow-up prompts by resuming the selected project's persistent
   Copilot conversation.
3. Store the current project output under revision `000`, while keeping the
   storage model and APIs ready for additional revisions later.
4. Display assumptions, source dates, and unanswered requirements.
5. Add cancellation, retry, and clearer generation progress.

## Priority 1 implementation plan

### Decisions

- One project owns one persistent Copilot conversation.
- Every project uses revision ID `000` for the current implementation.
- Follow-up generation atomically replaces the content of revision `000`.
- The project browser lists projects, not revisions.
- Revision IDs remain part of models, storage paths, repository keys, and use
  case contracts so multiple immutable revisions can be added later without
  redesigning project persistence.
- Each project stores its last-used model and theme selections and reapplies
  them when creating or resuming the conversation.

### Local storage

Use a dedicated `projects.db` database. Do not add project data to
`data\azhst.db`, because the sample-question database can be deleted and
recreated when it is invalid or incompatible.

Initial project storage:

```text
generated\
  <project-id>\
    000\
      index.html
      presentation.pptx
```

The initial database model should contain:

```text
projects
- id
- title
- original_query
- copilot_session_id
- created_utc
- updated_utc
- selected_model_id
- html_theme_id
- presentation_theme_id

project_revisions
- project_id
- revision_id
- html_file_path
- presentation_file_path
- generation_status
- updated_utc
```

`project_revisions` has exactly one row with revision ID `000` per project for
now. A composite key of `(project_id, revision_id)` prepares the database for
future revision history without exposing that complexity in the UI.

### Copilot conversation lifecycle

The existing standalone query assessment remains a short-lived isolated
session. It should not become part of the project conversation because its
system instructions and response contract are different.

After a question passes assessment:

1. Create the project record and a stable Copilot session ID derived from the
   project ID.
2. Create the visualization session with that explicit `SessionId`.
3. Keep `CopilotClientMode.Empty`, the empty tool allowlist, disabled session
   telemetry, and no host filesystem tools.
4. Send the initial visualization request through the project session.
5. Secure and validate the generated HTML before saving revision `000`.
6. Dispose the SDK session without deleting it so its transcript remains
   resumable.

When reopening a project or sending a follow-up:

1. Load the project and revision `000`.
2. Resume the stored session with `ResumeSessionAsync`.
3. Reapply the selected model, current visualization system instructions, and
   empty tool restrictions.
4. Send the follow-up as the next user turn.
5. Process the complete returned HTML through the existing security, theme,
   icon, and presentation-manifest validation pipeline.
6. Atomically replace `000\index.html` only after validation succeeds.
7. Clear the current PowerPoint association because it no longer represents
   the updated visualization; the user can rebuild it on demand.

The SDK's shared cross-session search store should remain disabled. Project
continuity uses the explicit session ID and session transcript persistence,
not cross-session retrieval.

### Application boundaries

Add application-layer models and ports:

```text
Project
ProjectRevision
IProjectRepository
IProjectArtifactStore
ICopilotProjectConversation
```

Add focused use cases:

```text
CreateProjectUseCase
OpenProjectUseCase
RefineProjectUseCase
DeleteProjectUseCase
```

Infrastructure implementations:

- `SqliteProjectRepository` stores projects and revision `000`.
- `FileProjectArtifactStore` performs atomic writes beneath the project and
  revision directory.
- `CopilotProjectConversation` creates, resumes, and deletes SDK sessions.

The Desktop layer adds:

- a project browser ordered by the most recently updated project
- a **New project** action
- a follow-up input when a project is open
- project rename and delete actions
- loading, empty, failed, and conversation-unavailable states

### Failure and deletion behavior

- A failed or cancelled follow-up must leave the existing revision `000`
  unchanged.
- If the persisted Copilot session cannot be resumed, show an explicit
  conversation-unavailable error rather than silently starting a context-free
  session.
- Deleting a project removes its project database records, local artifact
  directory, and persisted Copilot session.
- Application shutdown disposes sessions but never deletes them.
- Project history is read from `projects.db`; SDK session listing is not the
  user-facing source of project metadata.

### Delivery sequence

1. Add project and fixed-revision models, SQLite persistence, and atomic
   project artifact storage.
2. Add the persistent Copilot conversation abstraction and SDK
   create/resume/delete implementation.
3. Convert new visualization generation into project creation with revision
   `000`.
4. Add the project browser and reopen behavior.
5. Add follow-up refinement that resumes the project conversation and replaces
   revision `000`.
6. Add project deletion, missing-session handling, cancellation, retry, and
   integration tests.

### Acceptance criteria

- A new question creates a project, persistent Copilot session, and revision
  `000`.
- Restarting AzHST preserves the project list and allows its conversation to
  continue.
- Every follow-up uses the same stored Copilot session ID.
- Successful follow-ups replace revision `000`; no revision history is shown
  or created.
- Failed or cancelled follow-ups preserve the previously valid visualization.
- Rebuilding PowerPoint uses the latest revision `000`.
- Deleting a project removes both local project data and its Copilot session.
