# TaskMaster - WPF TaskSpec Generator

A comprehensive WPF application that creates high-quality Markdown task specifications for AI code agents (Claude CLI) to implement in new branches with stepwise commits and auto-opened pull requests.

## Features

### Core Functionality
- **Project-Aware Task Management**: Each task belongs to a selected project with per-project numbering
- **AI-Powered Inference**: Uses Claude to infer task specification fields from just a title and summary
- **Markdown Generation**: Creates correctly formatted specifications following strict templates
- **Git Integration**: Seamless integration with Git repositories and GitHub CLI
- **SQLite Catalog**: Local metadata storage with Git-friendly JSON exports

### Task Specification Process
1. **Select Project** from dropdown or create new project
2. **Enter Title + Summary** - minimal input required
3. **Provide CLAUDE.md Path** - project context file (optional)
4. **Claude Inference** - automatically generates detailed fields
5. **Preview & Edit** - live Markdown preview with editable fields
6. **Save Spec** - generates numbered specification file
7. **Run Panel** - creates design decision document
8. **Run Update** - implements task with Claude CLI

### Advanced Features
- **Project Template Generation**: Set up TaskMaster structure in any repository
- **Import/Export Templates**: Share project configurations between repositories
- **Folder Browser Integration**: Intelligent directory and file selection
- **Validation & Error Handling**: Comprehensive input validation and error reporting
- **Real-time Preview**: Live Markdown preview of generated specifications

## Installation

### Prerequisites
- .NET 8.0 or later
- Claude Code CLI installed and authenticated
- Git (for repository operations)
- GitHub CLI (optional, for PR creation)

### Claude Code CLI Installation
1. Install Claude Code CLI: `npm install -g @anthropic-ai/claude-code`
2. Authenticate: `claude setup-token` or interactive login
3. Verify installation: `claude --version`
4. Test functionality: `claude --print "Hello, Claude!"`

### Build from Source
```bash
git clone <repository-url>
cd taskmaster
dotnet build
dotnet run
```

## Usage

### Initial Setup
1. **Launch TaskMaster**
2. **Create or Select Project**
3. **Set Repository Root** - point to your Git repository
4. **Browse for CLAUDE.md** - select your project's instruction file
5. **Setup Project Structure** - click "Setup Project" to generate required directories

### Creating Task Specifications
1. **Enter Task Details**:
   - Title (required)
   - Summary (required, minimum 10 characters)
   - CLAUDE.md path (optional but recommended)

2. **Generate Specification**:
   - Click "Infer Spec Fields"
   - Review and edit the generated fields
   - Customize acceptance criteria, test plan, scope paths

3. **Save and Implement**:
   - Click "Save Spec" to create the markdown file
   - Click "Run Panel" to generate design decisions
   - Click "Run Update" to implement the task

### Project Management
- **Export Catalog**: Generate JSON snapshot of all projects and tasks
- **Export Template**: Save project configuration for reuse
- **Import Template**: Load project from exported template
- **Setup Project**: Generate TaskMaster directory structure

## File Structure

TaskMaster generates the following structure in your repository:

```
your-repo/
├── docs/
│   ├── specs/          # Task specifications (YYYYMMDD-slug.md)
│   ├── decisions/      # Design decisions (YYYYMMDD-slug.md)
│   └── README.md       # Documentation overview
├── .claude/            # Claude CLI slash commands
│   ├── infer-taskspec.md
│   ├── panel-wpf-decide.md
│   └── update-wpf-spec.md
└── CLAUDE.md           # Project instructions (if generated)
```

## Claude CLI Integration

TaskMaster uses custom slash commands for Claude CLI integration:

### `/infer-taskspec`
Infers detailed task specification fields from basic input:
```bash
claude /infer-taskspec --title "Feature Title" --summary "Brief description" --project "ProjectName"
```

### `/panel-wpf-decide`
Runs design debate panel and generates decision document:
```bash
claude /panel-wpf-decide docs/specs/20250920-feature-slug.md
```

### `/update-wpf-spec`
Implements the task based on specification and decision:
```bash
claude /update-wpf-spec docs/specs/20250920-feature-slug.md
```

## Specification Format

Generated task specifications follow this template:

```markdown
# Task Title

**Number:** #42
**Type:** feature
**Project:** ProjectName
**Created:** 2025-09-20

## Scope Paths
- `src/components/Feature.tsx`
- `src/services/FeatureService.ts`

## Required Docs
- `docs/api/feature-api.md`

## Summary
Detailed task description...

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2

## Test Plan
- Unit tests for components
- Integration tests for service

## Notes
Additional implementation notes...
```

## Database Schema

TaskMaster uses SQLite with the following tables:

### Projects
- `Id` (Primary Key)
- `Name` (Unique)
- `TaskCount` (Auto-incrementing)
- `LastUpdated`
- `ClaudeMdPath`
- `Metadata` (JSON)

### TaskSpecs
- `Id` (Primary Key)
- `ProjectId` (Foreign Key)
- `Number` (Per-project sequential)
- `Title`, `Summary`, `Type`
- `AcceptanceCriteria`, `TestPlan` (JSON arrays)
- `ScopePaths`, `RequiredDocs` (JSON arrays)
- `Notes`, `SuggestedTasks`, `NextSteps`

## Export Format

JSON exports follow this structure:

```json
{
  "exportedAt": "2025-09-20T10:00:00Z",
  "projects": [
    {
      "name": "ProjectName",
      "taskCount": 42,
      "lastUpdated": "2025-09-20T09:00:00Z",
      "claudeMdPath": "/path/to/CLAUDE.md",
      "tasks": [
        {
          "id": 42,
          "title": "Task Title",
          "slug": "task-slug",
          "type": "feature",
          "status": "todo",
          "created": "2025-09-20T08:00:00Z",
          "summary": "Task description",
          "acceptanceCriteria": ["criterion 1"],
          "testPlan": ["test step 1"]
        }
      ]
    }
  ]
}
```

## Configuration

### Application Settings
- Database location: `%LocalAppData%\TaskMaster\taskmaster.db`
- Temporary files: System temp directory
- Project templates: JSON format

### Claude CLI Requirements
- Claude CLI must be installed and authenticated
- Slash commands are copied to target repositories
- Working directory set to repository root for all operations

## Troubleshooting

### Common Issues
1. **"Failed to get response from Claude"**
   - Ensure Claude CLI is installed and authenticated
   - Check that slash commands exist in `.claude/` directory
   - Verify network connectivity

2. **"Infer Spec button not clickable"**
   - Ensure project is selected
   - Verify title and summary are not empty
   - Check that summary is at least 10 characters

3. **"Invalid Git repository"**
   - Ensure selected directory contains `.git` folder
   - Verify repository is properly initialized

### Validation Errors
- **Title required**: Must not be empty, max 200 characters
- **Summary required**: Minimum 10 characters
- **CLAUDE.md path**: File must exist if specified
- **JSON fields**: Scope paths, criteria, test plan must be valid JSON arrays

## Development

### Architecture
- **MVVM Pattern**: Clean separation of concerns
- **Dependency Injection**: Services injected into ViewModels
- **Async/Await**: All I/O operations are asynchronous
- **Error Handling**: Comprehensive exception handling with user feedback

### Key Services
- `DatabaseService`: SQLite operations
- `ClaudeService`: Claude CLI integration
- `SpecFileService`: Markdown generation and file operations
- `ValidationService`: Input validation and Git repository checks
- `ProjectTemplateService`: Template generation and project setup
- `FolderBrowserService`: File and directory selection

### Testing
Run the application and test:
1. Project creation and selection
2. Task specification inference
3. Markdown generation and preview
4. File operations and directory structure
5. Export/import functionality

## Contributing

This application serves as the "brain child" for TaskMaster functionality and can be extended to work with other repositories and project types. The modular architecture supports easy customization and enhancement.

### Extension Points
- Custom slash commands for different project types
- Additional validation rules
- Enhanced export formats
- Integration with other version control systems
- Support for different AI providers

## License

[Specify your license here]

## Support

For issues and feature requests, please refer to the project repository or contact the development team.