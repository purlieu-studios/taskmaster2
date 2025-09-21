# WPF TaskSpec Generator — Functional & Technical Description (Long)

## 1) Purpose (why this app exists)

We’re building a **WPF application** that creates **high-quality Markdown task specs** for a repo. Each spec is a precise, machine-digestible instruction set for an AI code agent (Claude CLI) to implement in a **new branch** with **small stepwise commits** and an **auto-opened PR**.

The app:

* **Is project-aware**: each task belongs to a selected Project; numbering is **per-project** and persisted in SQLite.
* Lets you provide only **two things**: a **Title** and a **Summary** (even a partial draft is fine).
* Injects a **CLAUDE.md reference doc** (from a separate project) into the inference pipeline, using a **file path that the user provides**.
* Uses Claude to **infer the rest of the spec fields** (Acceptance Criteria, Test Plan, Scope Paths, etc.) automatically, as well as to propose **related or dependent tasks for that specific project**.
* Displays the inferred fields and suggested tasks in the form so you can adjust/edit them before saving.
* Generates a **correctly formatted Markdown spec** with **deterministic slugging/numbering** based on the project’s task counter.
* **Invokes Claude CLI** to run a design debate (panel) and/or implementation (update) with **one click**.
* Stores metadata in a **local catalog** (SQLite) and exports a **Git-friendly JSON snapshot**. Projects are stored separately, and underneath each project are lists of tasks. Each project tracks the **number of tasks created**, ensuring unique sequential IDs.
* Uses Claude to also **suggest next steps / follow-up tasks** inferred from the current project state and CLAUDE.md, so planning becomes semi-automated.

## 2) What the app produces

* A Markdown file under `docs/specs/YYYYMMDD-<slug>.md` that follows a strict template:

  * Title, Number, Type, Project, Created
  * Scope Paths, Required Docs
  * Summary, Acceptance Criteria, Non-Goals (optional), Test Plan, Notes
* Suggested **related tasks** will be captured either inline under Notes or exported separately as new draft specs for review.
* Suggested **next steps** will be inferred using Claude, ensuring continuity and roadmap generation across tasks.
* Downstream artifacts created by Claude:

  * `docs/decisions/YYYYMMDD-<slug>.md` (panel decision; **same filename as the spec, but under `docs/decisions/`**)
  * Branch `task/<Number>-<slug>`
  * PR titled `[#<Number>] <Title>` with body based on the spec

## 3) End-to-end flow (user POV)

1. **Select Project** from a dropdown (populated from SQLite). Create a new Project inline if needed.
2. **Enter Title + Summary only.**
3. **Provide CLAUDE.md file path.**
4. **Claude inference**: App sends Title, Summary, Project context, and CLAUDE.md to Claude → returns full fields + suggested related tasks + suggested next steps.
5. **Preview & Edit**: Live Markdown preview (text). You can tweak any inferred field.
6. **Save Spec** (project-scoped numbering):

   * Reserve the **next per-project Number** transactionally (e.g., `#37`).
   * Render Markdown; write atomically to `docs/specs/{UtcNow:yyyyMMdd}-{slug}.md`.
   * Upsert Task metadata in catalog; update the Project’s **task count**.
   * Optionally store suggested related tasks and next steps as **DraftTask** rows under the same Project.
7. **Run Panel** → generates `docs/decisions/{UtcNow:yyyyMMdd}-{slug}.md` and executes `claude /panel-wpf-decide <spec>`.
8. **Run Update** → uses the decision file from step 7 and executes `claude /update-spec <decision>` (or `/update-wpf-spec`).
9. **Catalog/Export**: JSON snapshot for Git diffs.

## 4) UI blueprint (MVVM)

* **MainWindow**

  * **Top bar**

    * Project dropdown (populated from SQLite)
    * Add Project button (creates new project entry in catalog)

  * **Left pane (Form)**

    * Title (text)
    * Summary (multiline, can be partial)
    * CLAUDE.md Path (file picker / text input — persisted per Project in catalog)
    * \[Other fields + suggested related tasks + suggested next steps auto-populated by Claude inference — editable after generation]
    * Project Metadata Editor (optional key/value fields so each Project can store its own CLAUDE.md path and any other required metadata)

  **Right pane**

  * **Markdown Preview** (read-only TextBox with monospaced font)
  * Buttons:

    * Save Spec
    * Copy **Panel** Command
    * Copy **Update** Command
    * Run Panel (spawns Claude process)
    * Run Update (spawns Claude process)
    * Export Catalog / Import Catalog

* **Validation**: inline messages under inferred fields; Save/Copy/Run disabled until Claude has produced a valid spec.

---

## 5) Metadata (for Catalog/Export)

Each JSON snapshot will include structured metadata so Git diffs show meaningful changes:

```json
{
  "projects": [
    {
      "name": "WPF TaskSpec Generator",
      "taskCount": 37,
      "lastUpdated": "2025-09-20T05:00:00Z",
      "tasks": [
        {
          "id": 37,
          "title": "Implement Catalog Export",
          "slug": "implement-catalog-export",
          "status": "todo",
          "created": "2025-09-20T05:00:00Z",
          "summary": "Add support for exporting catalog as JSON for Git diffs.",
          "acceptanceCriteria": ["Export JSON file alongside SQLite catalog", "Include task/project metadata"],
          "testPlan": ["Validate JSON export matches catalog", "Check Git diff readability"]
        }
      ]
    }
  ]
}
```

This ensures JSON exports are **human-readable**, **diff-friendly**, and act as a reliable snapshot of the catalog state.

---

## 6) Generation Triggers & Timing

**Deterministic, button-driven flow (no hidden auto-generation):**

1. **Infer Spec** (explicit)

   * Trigger: Click **Infer** (or **Generate**) after selecting **Project**, entering **Title + Summary**, and providing **CLAUDE.md path**.
   * Action: Calls Claude with Title, Summary, CLAUDE.md, and **Project context** (recent tasks from catalog) to produce: Type, Scope Paths, Acceptance Criteria, Test Plan, Required Docs, and **Suggested next-step tasks**.
   * Caching: The app stores the prompt + response and marks the inference **stale** whenever Title, Summary, Project, or CLAUDE.md path changes. A **Regenerate** badge appears when stale.

2. **Save Spec** (explicit)

   * Trigger: Click **Save Spec** after a valid inference.
   * Action: **Reserves next per-project Number** transactionally → renders Markdown → atomic write to `docs/specs/{yyyyMMdd}-{slug}.md` → upserts catalog → exports JSON snapshot.

3. **Run Panel** (explicit)

   * Trigger: Click **Run Panel**.
   * Action: Executes `claude /panel-wpf-decide <specPath>`.
   * Output: Writes decision to `docs/decisions/{yyyyMMdd}-{slug}.md` (mirrors spec filename).
   * Optional: After panel, you may **Regenerate suggestions** using the decision as extra context (togglable).

4. **Run Update** (explicit)

   * Trigger: Click **Run Update**.
   * Precondition: Matching decision file **exists**.
   * Action: Executes `claude /update-spec <specPath>` (or `/update-wpf-spec`) and the CLI **consumes** `docs/decisions/{yyyyMMdd}-{slug}.md` to implement, commit, push, and open a PR.

5. **Suggested Tasks Generation**

   * Default: Produced alongside **Infer Spec** using CLAUDE.md + recent Project tasks.
   * Optional: Re-run suggestions **post-panel** to adjust roadmap based on the decision.

6. **When things change**

   * Changing **Project**, **Title**, **Summary**, or **CLAUDE.md path** → marks inference **stale** and disables Save/Panel/Update until you re-run **Infer**.
   * Editing inferred fields does **not** force re-inference; Save proceeds with edits.

---

## 7) Claude CLI Requirements & Contract

**Required commands (CLI must provide):**

* `claude /panel-wpf-decide <specPath>` → emits `docs/decisions/{yyyyMMdd}-{slug}.md`
* `claude /update-spec <specPath>` (or `/update-wpf-spec`) → reads matching decision, creates `task/<Number>-<slug>` branch, small commits, push, open PR

**Arguments & environment the app supplies:**

* **Working directory**: set to repo root (so relative paths resolve)
* **Spec path**: `docs/specs/{yyyyMMdd}-{slug}.md`
* **Decision path**: `docs/decisions/{yyyyMMdd}-{slug}.md`
* **Branch name** (for update): `task/<Number>-<slug>`
* **Project context file** (temp JSON): recent task metadata for suggestions (path passed via `--project-context`)
* **Repo root**: passed via `--repo-root` (or env `CLAUDE_REPO_ROOT`)
* **Decisions dir / Specs dir**: `--decisions-dir`, `--specs-dir` (or envs `CLAUDE_DECISIONS_DIR`, `CLAUDE_SPECS_DIR`)

**Preflight checks (WPF app enforces before running CLI):**

* Git installed; **clean working tree** (or user agrees to auto-stash)
* Remote `origin` exists and is writable
* `gh` (GitHub CLI) authenticated if PR auto-open is desired; otherwise the CLI must print the PR URL
* Matching **decision file exists** before **Update**
* **CLAUDE.md** file exists at the provided path
* Optional timeout (e.g., `--timeout 20m`)

**Exit codes & logging:**

* `0` = success; nonzero = failure. The app shows first \~20 lines of stderr with a **Copy full log** button.
* The app captures stdout/stderr to an **Output pane**; on success, it parses **PR URL** for an **Open PR** button.

**Rate limits / sessions:**

* User must be **logged in** to Claude CLI (no API key), and aware of any session/time limits. The app should surface CLI errors (e.g., time cap reached) verbatim.

**Claude CLI Guardrails — Safety Contract**:

The WPF app enforces **non-negotiable guardrails** to prevent runaway behavior, unsafe Git operations, and non-deterministic outputs. These guardrails ensure every change is small, reviewable, and reversible.

**Non-Negotiable Default Flags** (hardwired into all CLI invocations):

* `--dry-run` (preview mode by default; user must explicitly disable to commit changes)
* `--small-commits` (enforce one step per commit)
* `--no-force` (block destructive Git commands like force-push)
* `--max-diff-lines=200` (cap patch size per commit)

These flags cannot be unchecked from the UI — they are hardwired defaults for safety.

**Deterministic Output Contract**:

After a successful run, the CLI must output:
* A **decision file** in `docs/decisions/YYYYMMDD-<slug>.md` (for panel runs)
* A **branch** named `task/<Number>-<slug>`
* A **PR title** formatted `[#<Number>] <Title>`
* A **PR body** populated with the spec contents

The WPF app validates these outputs. If any artifact is missing or malformed, the run is marked as failed.

**Enhanced Preflight Checks** (enforced before CLI execution):

* **Clean Git tree**: no uncommitted changes (auto-stash offered if dirty)
* **Writable remote**: origin available and authenticated via GitHub CLI
* **Spec file exists**: referenced markdown spec is present and valid
* **Decision file exists**: required for `/update-spec` invocations
* **CLAUDE.md path valid**: reference doc exists and is readable
* **Disk space check**: sufficient space for repo operations
* **Network connectivity**: GitHub API reachable

**Post-Run Validation** (enforced after CLI execution):

* Inspect generated files for correct naming/location
* Ensure Git history shows atomic commits under the correct branch
* Verify PR metadata matches the spec
* Validate decision file structure and content

If validation fails, the app alerts the user and rolls back any partial changes.

**Extensibility** (future guardrails):

* `--require-tests` (block PR if no test files touched)
* `--lint` (auto-run linter; block if errors)
* `--schema-validate` (validate spec/decision JSON structure)
* `--max-execution-time` (timeout for long-running operations)

**Summary**: Guardrails are not optional. They guarantee every Claude CLI run from the WPF app is **safe, reviewable, deterministic, and reversible**.

---

## TL;DR (do this, then ship)

1. **User chooses a project from dropdown (SQLite-backed).**
2. Each project tracks how many tasks have been generated; new specs increment the counter.
3. User enters Title + Summary, provides CLAUDE.md path.
4. Claude infers the rest of the spec **and suggests related tasks and next steps**.
5. Save → Panel (decision file) → Update (based on decision) → Catalog (Git-pushed SQLite with per-project task counters + JSON metadata exports).

This document is the single source of truth for how the WPF app behaves.
