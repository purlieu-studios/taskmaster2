# TaskMaster Architecture

## Overview
TaskMaster is a WPF application that generates high-quality task specifications and automates their implementation through Claude CLI integration.

## Layer Architecture

### 1. WPF Application Layer (`TaskMaster.exe`)
- **Views**: Pure XAML with no code-behind logic
- **ViewModels**: MVVM pattern, handles UI logic and binding
- **Services**: Business logic and external integrations

### 2. Core Library (Future separation)
- **Models**: Domain objects (TaskSpec, Project, etc.)
- **Services**: UI-agnostic business logic
- **Data Access**: Database and file operations

### 3. Test Layer
- **Unit Tests**: ViewModel and service testing
- **Integration Tests**: End-to-end workflows

## Key Components

### Services
- **ClaudeService**: Claude CLI integration and inference
- **DatabaseService**: SQLite operations for projects/tasks
- **SpecFileService**: Markdown generation and file management
- **PanelService**: Expert panel orchestration
- **DecisionFileService**: Decision artifact management
- **GuardrailsService**: Safety and validation enforcement

### Models
- **TaskSpec**: Core task specification entity
- **Project**: Project container with task numbering
- **ClaudeInferenceRequest/Response**: Claude API contracts
- **PanelResult**: Panel execution results

### ViewModels
- **MainViewModel**: Primary application state and commands

## MVVM Principles
1. **Views** contain only XAML bindings, no logic
2. **ViewModels** handle all UI state and commands
3. **Services** contain all business logic
4. **Models** are pure data containers

## Data Flow
1. User input → ViewModel
2. ViewModel → Service calls
3. Service → External APIs (Claude CLI, SQLite)
4. Results → ViewModel properties
5. ViewModel → View updates via binding

## File Organization
```
TaskMaster/
├── Views/           # XAML files only
├── ViewModels/      # MVVM ViewModels
├── Services/        # Business logic
├── Models/          # Data structures
├── docs/           # Documentation and decisions
├── .claude/        # Claude CLI configuration
└── scripts/        # Automation scripts
```

## Dependencies
- **WPF**: UI framework
- **CommunityToolkit.Mvvm**: MVVM helpers
- **Microsoft.Data.Sqlite**: Database access
- **Newtonsoft.Json**: JSON serialization

## Design Patterns
- **MVVM**: UI separation of concerns
- **Service Layer**: Business logic encapsulation
- **Repository**: Data access abstraction
- **Command**: User action encapsulation

## Testing Strategy
- **ViewModel Testing**: Mock services, test commands
- **Service Testing**: Unit tests for business logic
- **Integration Testing**: Full workflow validation