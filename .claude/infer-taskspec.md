# Infer TaskSpec Fields

## Description
Infer detailed task specification fields based on a title, summary, project context, and recent tasks.

## Parameters
- `title` (required): The task title
- `summary` (required): The task summary
- `project` (required): The project name
- `claude-md` (optional): Path to CLAUDE.md file for project context
- `recent-tasks` (optional): JSON array of recent tasks for context

## Usage
```
/infer-taskspec --title "Add user authentication" --summary "Implement login/logout functionality" --project "TaskMaster"
```

## Response Format
The command should return a JSON response with the following structure:

```json
{
  "type": "feature|bug|enhancement|refactor|docs",
  "scopePaths": ["path/to/file1", "path/to/file2"],
  "acceptanceCriteria": ["criterion 1", "criterion 2"],
  "testPlan": ["test step 1", "test step 2"],
  "requiredDocs": ["doc1.md", "doc2.md"],
  "nonGoals": "Optional: what this task explicitly does NOT include",
  "suggestedTasks": [
    {"title": "Task title", "summary": "Brief description", "type": "feature"}
  ],
  "nextSteps": [
    {"title": "Next step title", "summary": "Brief description", "type": "feature"}
  ]
}
```

## Guidelines
- Be specific and actionable in acceptance criteria
- Include relevant file paths in scopePaths based on the project context
- Suggest 2-3 related tasks that would complement this one
- Suggest 1-2 logical next steps after this task is completed
- Keep suggestions focused on the project domain