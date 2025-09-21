# Design Decision: Add Export Template Feature

**Spec:** docs/specs/20250920-add-export-template-feature.md
**Date:** 2025-09-20
**Generated:** 2025-09-20 14:45:00 UTC

---

## Panel Execution Summary

**Question:** How should we implement this spec safely and minimally?
**Phase:** decide
**Rounds:** 2
**Scope:** src/

### Round 0 — Scan

- Files impacted: Services/, ViewModels/, Views/ layers following MVVM
- No layer violations detected with proposed approach
- Async I/O risks with zip creation operations need progress reporting
- Missing tests identified for file sanitization logic
- Guardrail compliance: small scope, atomic commits, testable design

### Round 1 — Options

**Option A:** Direct file system approach with System.IO.Compression
- Use built-in .NET zip libraries for archive creation
- Simple recursive directory traversal with hardcoded filters
- Export dialog shows static file list preview

**Option B:** Service-based template engine with configurable profiles
- Abstract template creation through configurable export profiles
- Support multiple output formats and customizable filters
- Dynamic preview generation based on selected profile

### Round 2 — Expert Debate

**Core Architect:** Option A maintains clean separation - TemplateService in Services layer, minimal UI coupling. Option B introduces premature abstraction for v1 scope.

**MVVM Enforcer:** Both options support MVVM correctly. Option A simpler with TemplateExportViewModel handling async commands, progress binding, no code-behind needed.

**Data & Persistence:** Export history tracking should use existing SQLite infrastructure. Option A allows simple export log table. Option B complicates with profile storage.

**Async & Performance:** Zip operations must be async with progress reporting. Option A straightforward with IProgress<T>. Both need cancellation support for large directories.

**Testing Lead:** Option A more testable - clear service boundaries, mockable file system operations. Option B harder to test with abstract profile system.

**Tooling & DX:** Option A integrates better with existing codebase patterns. System.IO.Compression is reliable, well-documented.

**Red Team:** Option A risks: zip corruption, file permission issues, path length limits on Windows. Option B adds complexity without clear v1 benefits.

**Release Manager:** Option A fits current release scope. Option B scope creep risk with profile system. Keep it simple for v1.

**Docs Writer:** Option A needs clear documentation on export format and usage. Option B would require complex profile documentation.

---

Decision:
Implement Option A using System.IO.Compression with a dedicated TemplateService for file operations, TemplateExportViewModel for UI logic, and simple export dialog with progress feedback. Create export history tracking using existing SQLite infrastructure with minimal schema addition.

Why:
- Maintains established MVVM architecture patterns without introducing new abstractions
- Uses proven .NET libraries for reliable zip creation and file operations
- Provides essential functionality while avoiding scope creep for v1 implementation
- Enables proper async operations with progress reporting and cancellation support
- Fits within existing project patterns for services, ViewModels, and database operations

Checklist:
1. Create TemplateService in Services/ with async zip creation methods
2. Add ExportTemplateDialog.xaml with progress bar and file preview list
3. Create TemplateExportViewModel with async commands and progress binding
4. Add ExportTemplateCommand to MainViewModel with proper error handling
5. Extend database schema with simple export_history table for tracking
6. Implement file filtering logic to exclude bin/, obj/, .vs/ directories

Tests:
- TemplateService_ValidProject_CreatesZipFile
- TemplateService_InvalidPath_ThrowsArgumentException
- TemplateService_LargeDirectory_ReportsProgressCorrectly
- ExportDialog_UserCancel_StopsOperation
- FileFilter_ExcludesBuildArtifacts_OnlyIncludesSourceFiles

Risks:
| Risk | Level | Mitigation |
|------|-------|------------|
| Zip corruption on large files | Medium | Validate zip integrity after creation, provide retry option |
| UI freeze during export | Medium | Use async/await with progress reporting, enable cancellation |
| File permission errors | Low | Try-catch with clear error messages, graceful degradation |
| Path length limits on Windows | Low | Use relative paths in zip, validate before creation |