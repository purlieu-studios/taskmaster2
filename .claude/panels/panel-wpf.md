# WPF Task Generator — Expert Panel

## Role Bindings
| Role                | File                                     |
|---------------------|-------------------------------------------|
| Core Architect      | .claude/roles/core-architect.md           |
| MVVM Enforcer       | .claude/roles/mvvm-enforcer.md            |
| Data & Persistence  | .claude/roles/data-persistence.md         |
| Async & Performance | .claude/roles/async-performance.md        |
| Testing Lead        | .claude/roles/testing-lead.md             |
| Tooling & DX        | .claude/roles/tooling-dx.md               |
| Release Manager     | .claude/roles/release-manager.md          |
| Red Team (Reviewer) | .claude/roles/red-team.md                 |
| Docs Writer         | .claude/roles/docs-writer.md              |

## Goal
Debate briefly and produce **one** decision with concrete deliverables for this repo.

## Params
- **question** (required): one-sentence decision ask
- **phase**: `decide` | `patch` | `roadmap` (default: `decide`)
- **rounds**: integer (default: `2`)
- **scope**: repo subpath to consider (default: `src/`)
- **max_lines**: max total patch lines when `phase=patch` (default: `30`)

## Baseline (must hold → else BLOCKED)
- Strict **MVVM**: no logic in code-behind; Views = XAML; logic in VMs; commands for actions.
- **Core** library is UI-agnostic; pure functions preferred; testable I/O (e.g., `System.IO.Abstractions`).
- Specs are **source of truth**; edit only inside **Scope Paths**.
- New/changed code must include **deterministic tests**.
- Always **branch** `task/<slug>`; open **PR** to `main`.

## Workflow
**Round 0 — Scan (≤5 bullets)**
- List files under `{scope}` impacted by the question.
- Flag MVVM or layering violations.
- Note async/I/O risks, potential perf hotspots, missing tests.
- Confirm spec `Required Docs` are present.
- Identify likely guardrail conflicts.

**Round 1 — Options**
- Provide exactly **2** viable approaches (≤3 sentences each).

**Rounds 2..N — Debate**
- In order: Core Architect → MVVM Enforcer → Data & Persistence → Async & Performance → Testing Lead → Tooling & DX.
- Red Team attacks both options vs Baseline and Guardrails.
- Release Manager flags sequencing, scope, branching/PR implications.
- Docs Writer notes any doc updates needed.

## Finalization (phase-aware)
- Pick **ONE** winner or output **BLOCKED** with explicit reason.
- **decide** → Output Decision/Why/Checklist/Tests/Risks (no diffs).
- **patch** → Output unified diffs for allowed files only, total ≤ `{max_lines}`; list test names. If exceed or files outside scope → **BLOCKED(Scope)** with plan.
- **roadmap** → Output next milestone, 6-step weekly plan, 5 tests, risks.

## Output Format (strict)

Decision:
<one paragraph>

Why:
- ...
- ...
- ...

Checklist:
1.
2.
3.
4.
5.
6.

Tests:
- ...
- ...
- ...
- ...
- ...

Risks:
| Risk | Level | Mitigation |
|------|-------|------------|
| ...  | ...   | ...        |

Patches:  <!-- phase=patch only; unified diffs; ≤max_lines total -->