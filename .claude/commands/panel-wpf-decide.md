# /panel-wpf-decide

## Purpose
Run the **WPF Expert Panel** to produce exactly ONE decision for a question or a given spec.
Outputs a decision artifact under `docs/decisions/`. **No code edits** here—use `/update-wpf-spec` to implement.

## Arguments
- **Required**: either
  - a **spec path** (e.g., `docs/specs/20250919-implement-minimal-ui.md`), or
  - a **quoted question string** (e.g., `"Should v1 include Markdown preview?"`)
- **Optional flags**:
  - `--rounds=<int>` (default: 2)
  - `--scope=<path>` (default: `src/`)

## Steps

1) **Read Context**
   - Always open:
     - `.claude/panels/panel-wpf.md`
     - all files in `.claude/roles/`
     - `docs/GUARDRAILS.md`
     - `CLAUDE.md` (project architecture)
   - If a **spec path** is provided:
     - Open the spec and its `Required Docs`.
     - Extract the **slug** from filename `YYYYMMDD-<slug>.md`.

2) **Derive Question & Output Path**
   - **Spec mode**:
     - question := "How should we implement this spec safely and minimally?"
     - decision file := `docs/decisions/YYYYMMDD-<slug>.md`
   - **Freeform mode** (no spec):
     - question := the provided string
     - create a **slug** from the question (lowercase, non-alnum → `-`, collapse/truncate to ~6 words)
     - decision file := `docs/decisions/YYYYMMDD-freeform-<slug>.md`

3) **Run Panel (phase=decide)**
   - Use `.claude/panels/panel-wpf.md` with:
     - `phase: decide`
     - `rounds: {--rounds or 2}`
     - `scope: {--scope or src/}`
     - `question: {derived question}`
   - Produce output in the panel's **strict format** (Decision / Why / Checklist / Tests / Risks).

4) **Write Artifact**
   - Write the exact panel output to the **decision file** path derived above.
   - Print the absolute path at the end for tooling to capture.

## Rules
- This command **must not edit code** or create branches/PRs.
- If information is missing or baseline would be violated, output **BLOCKED** with reason and write that to the decision file.
- Keep the debate focused; no more than two options before choosing one.

## Examples

**From a spec:**
```
/panel-wpf-decide docs/specs/20250920-add-user-auth.md
```

**Freeform question:**
```
/panel-wpf-decide "Should we use Entity Framework or raw SQLite?"
```

## Output
Creates decision file with strict format:
- Decision: (one paragraph)
- Why: (bullet points)
- Checklist: (numbered steps)
- Tests: (test requirements)
- Risks: (risk/level/mitigation table)