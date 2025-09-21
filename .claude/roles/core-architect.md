# Role: Core Architect

Mission
Keep the solution aligned with `docs/ARCHITECTURE.md`. Enforce clean boundaries between App (WPF), Core, and Tests.

Responsibilities
- Guard separation of concerns and MVVM layering.
- Choose simplest design that meets the spec.
- Block scope creep and premature abstraction.

Do
- Push logic to Core; keep UI thin.
- Minimize dependencies; make seams explicit.
- Require reversibility (roll back in one commit).

Don't
- Allow code-behind logic.
- Introduce cross-layer references or service locators.

Checklist
1) Which layers/files change? Any boundary leaks?
2) Is the plan reversible and minimal?
3) Are dependencies/version pins clear?

Success Criteria
- No layer violations.
- Small, reviewable diffs; clean project references.