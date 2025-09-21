# Add Export Template Feature

**Number:** #1
**Type:** feature
**Project:** WPF TaskSpec Generator
**Created:** 2025-09-20

## Scope Paths

- `Services/TemplateService.cs`
- `ViewModels/TemplateExportViewModel.cs`
- `Views/ExportTemplateDialog.xaml`
- `ViewModels/MainViewModel.cs`

## Required Docs

- `CLAUDE.md`
- `docs/ARCHITECTURE.md`
- `docs/GUARDRAILS.md`

## Summary

Create functionality to export project structure and CLAUDE.md as templates for new repositories. Users should be able to package their TaskMaster setup (including .claude directory, CLAUDE.md, and project structure) into a distributable template that others can use to bootstrap new projects with the same configuration.

## Acceptance Criteria

- [ ] User can click "Export Template" button in the main interface
- [ ] Export dialog shows preview of files to be included
- [ ] Template export creates a zip file with sanitized content
- [ ] Exported template includes CLAUDE.md and .claude/ directory structure
- [ ] File paths are sanitized to remove machine-specific information
- [ ] Export operation shows progress feedback for large projects
- [ ] Export history is tracked for user reference

## Non-Goals

- Multi-format export (only zip for v1)
- Template marketplace or sharing features
- Dynamic template customization during export

## Test Plan

- [ ] Build passes after implementation
- [ ] TemplateService unit tests validate zip creation
- [ ] Export dialog UI tests confirm proper MVVM binding
- [ ] Integration test validates exported template can initialize new project
- [ ] File sanitization tests ensure no sensitive data leaks
- [ ] Progress reporting works correctly for large directories
- [ ] Export history tracking functions properly

## Notes

This feature enables TaskMaster users to share their project configurations and setups with team members or open source projects. The exported templates will help standardize project structures across organizations and facilitate onboarding new developers to established patterns.

Consider future enhancements like template validation, custom export profiles, and integration with git repository initialization.