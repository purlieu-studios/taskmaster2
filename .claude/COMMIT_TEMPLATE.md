# Commit Template

## Format
```
<type>(<scope>): <short description>

Why:
- <reason 1>
- <reason 2>

Changes:
- <file/area 1>
- <file/area 2>

Tests:
- <test name 1>
- <test name 2>

Refs: <spec-path> <decision-path>
```

## Types
- feat: new feature
- fix: bug fix
- refactor: code restructuring
- test: adding/updating tests
- docs: documentation changes
- chore: maintenance tasks

## Examples
```
feat(wpf): add task inference dialog

Why:
- Enable automated spec field generation
- Reduce manual entry for common patterns

Changes:
- Views/InferenceDialog.xaml
- ViewModels/InferenceViewModel.cs
- Services/ClaudeService.cs

Tests:
- InferenceViewModel_ValidInput_GeneratesSpec
- ClaudeService_InferenceCall_ReturnsValidResponse

Refs: docs/specs/20250920-add-inference.md docs/decisions/20250920-add-inference.md
```