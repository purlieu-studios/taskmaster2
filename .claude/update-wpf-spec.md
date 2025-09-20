# Update WPF Spec

## Description
Implement a task specification for a WPF application based on the spec and its corresponding decision document. Creates a new branch, implements the feature with small commits, and opens a pull request.

## Parameters
- `spec-path` (required): Path to the task specification markdown file
- `repo-root` (optional): Repository root directory (defaults to current directory)
- `dry-run` (optional): Preview changes without making them
- `small-commits` (optional): Enforce one-plan-step-per-commit (default: true)
- `no-force` (optional): Avoid destructive git operations (default: true)
- `max-diff-lines` (optional): Limit diff size as a guardrail

## Usage
```
/update-wpf-spec docs/specs/20250920-add-user-authentication.md
/update-wpf-spec docs/specs/20250920-add-user-authentication.md --dry-run
```

## Prerequisites
- Matching decision file must exist at `docs/decisions/{same-filename-as-spec}.md`
- Git repository with clean working tree (or user agrees to auto-stash)
- Remote `origin` exists and is writable
- `gh` (GitHub CLI) authenticated for PR creation

## Process
1. Read the task specification and decision document
2. Create a new branch: `task/{Number}-{slug}`
3. Implement the feature following the decision guidance
4. Make small, focused commits for each implementation step
5. Push the branch to remote
6. Create a pull request with title: `[#{Number}] {Title}`

## Branch Naming
- Format: `task/{Number}-{slug}`
- Example: `task/42-add-user-authentication`

## Commit Strategy
- Make one commit per logical implementation step
- Use descriptive commit messages that reference the task
- Follow conventional commit format when possible

## Pull Request
- **Title**: `[#{Number}] {Title}`
- **Body**: Based on the task specification, including:
  - Summary
  - Acceptance criteria checklist
  - Link to decision document
  - Testing notes

## Safety Features
- Validates git repository state before starting
- Creates backup branch before major changes
- Enforces reasonable diff size limits
- Provides dry-run option for preview
- Graceful error handling with rollback capability

## Exit Codes
- `0`: Success
- `1`: Invalid arguments or missing prerequisites
- `2`: Git operation failed
- `3`: Implementation failed
- `4`: PR creation failed