# Panel WPF Decide

## Description
Run a design debate panel to analyze and make decisions about a WPF TaskSpec. This command reads a task specification and generates a design decision document.

## Parameters
- `spec-path` (required): Path to the task specification markdown file
- `repo-root` (optional): Repository root directory (defaults to current directory)

## Usage
```
/panel-wpf-decide docs/specs/20250920-add-user-authentication.md
```

## Process
1. Read and analyze the task specification
2. Consider implementation approaches for WPF applications
3. Evaluate technical decisions, architecture patterns, and dependencies
4. Generate a decision document with recommendations

## Output
Creates a decision file at `docs/decisions/{same-filename-as-spec}.md` with:
- **Decision Summary**: Key decisions made
- **Implementation Approach**: Recommended technical approach
- **Architecture Considerations**: WPF-specific architectural decisions
- **Dependencies**: Required libraries, frameworks, or tools
- **Risks and Mitigations**: Potential issues and how to address them
- **Next Steps**: Specific implementation guidance

## Decision File Format
```markdown
# Design Decision: {Task Title}

**Spec:** {spec-path}
**Date:** {current-date}

## Decision Summary
Brief summary of key decisions made.

## Implementation Approach
Detailed technical approach for implementation.

## Architecture Considerations
WPF-specific architectural decisions and patterns to use.

## Dependencies
Required libraries, frameworks, or tools.

## Risks and Mitigations
Potential issues and mitigation strategies.

## Next Steps
Specific guidance for implementation.
```