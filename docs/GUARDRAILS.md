# GUARDRAILS

This file defines the **quality policy** for contributions.
Unlike `CONTRACT.md` (strict interface rules), these guardrails are **review and CI standards**. They may evolve, but all must be enforced during code review and automated pipelines.

---

## 🚫 Absolute Rules

1. **Never modify files outside the listed `Scope Paths`** of a task spec.
2. **Never invent APIs, classes, or methods** — if unsure, ask or stop.
3. **Do not perform large-scale refactors** unless explicitly instructed.
4. **Do not touch security, authentication, or encryption code** without approval.
5. **Do not delete existing tests** unless asked; prefer updating or adding.

---

## ✅ Required Behaviors

1. **Always open and quote from `Required Docs`** before making changes.
2. **Always implement or update a test plan** for each change.
3. **Follow the established project architecture and patterns** (see `ARCHITECTURE.md`).
4. **Keep changes atomic**: small, reviewable diffs, not sweeping changes.
5. **Ask for clarification** if the task spec is unclear or context is missing.

---

## 🧪 Testing

* Every feature or fix must include tests.
* Run all tests after changes; do not commit failing code.
* Ensure coverage for both **success** and **failure** cases.

---

## 📐 Code Quality

* Match project coding style and conventions.
* Prefer clarity over cleverness — readable code first.
* Add documentation or comments if code behavior is non-obvious.

---

## 🔒 Security Practices

* Never log secrets, credentials, or tokens.
* Use existing secure helpers for config and environment access.
* Do not weaken or bypass validation, sanitization, or error handling.

---

## ⚡ Performance

* Avoid introducing unnecessary allocations or blocking calls.
* Favor async/await for I/O-bound work.
* Benchmark before adding "optimizations."
* No O(n²) or worse code paths without justification.

---

## 📂 Documentation

* Update related docs (`ARCHITECTURE.md`, feature specs) when code behavior changes.
* Add inline comments for complex logic, referencing the spec if relevant.

---

## 🛠 Workflow

* Prefer small diffs (< 100 lines) unless otherwise approved.
* When in doubt, pause and ask before proceeding with risky changes.
* If a change touches multiple projects, coordinate with project leads.
* One spec → one branch → one patch → one PR.
* PRs must stay within the scope defined by the spec.
* Max 20 files per PR unless the spec allows.

---

## 📑 Commits & PRs

* **Commit Message Format:** `<type>(<scope>): <short description>`

  * Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`.
  * Example: `feat(wpf): add About dialog window`
* **Branch Naming:** `task/<slug>` (slug from spec filename).
* **PR Title:** `<Type>: <Spec Title>` (e.g. `Feat: Add User Login`).
* **PR Body Template:**

  ```markdown
  ## Summary
  <Auto-fill from spec Summary>

  ## Acceptance Criteria
  - [ ] <list from spec>

  ## Test Plan
  - [ ] Build passes
  - [ ] Unit + integration tests added/updated
  - [ ] Manual checks (if any)

  ## Notes
  <Anything from spec Notes>
  ```

---

### Commit Granularity
- One **plan step = one commit**. Do not lump multiple steps together.
- Max **6 files** and **250 insertions** per commit (unless spec explicitly allows).
- Each commit must include:
  - `Why` (1–3 bullets),
  - `Changes` (files/areas),
  - `Tests` (names),
  - `Refs` (spec + decision).
- Never squash on merge; preserve step history.


## CI & Review Gates

* PR must pass build + test + coverage in GitHub Actions.
* CI must enforce dependency allowlist.
* CI must block if new binaries > 1MB are added without approval.
* Reviewers must verify acceptance criteria from the spec.

---

## Dependencies

* Only use packages explicitly whitelisted in the spec.
* No transitive additions without explicit approval.
* No preview/beta packages unless justified.
* Pinned versions required for reproducibility.

---

## Reproducibility

* SDK pinned in `global.json`.
* `dotnet restore` must succeed without private feeds.
* Specs, patches, and PRs must be linked.
* No machine-local assumptions (absolute paths, OS-specific configs).

---

## Observability

* Logging must use the shared logging abstraction.
* No `Console.WriteLine` in production code.
* Exceptions must be caught and wrapped with context.

---

## Failure Policy

* Any guardrail violation = **hard fail** in CI or mandatory PR rejection.
* Exceptions require explicit, documented approval in the PR description.