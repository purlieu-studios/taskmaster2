@echo off
REM TaskMaster Branch-Commit-PR Command
REM Usage: .\scripts\branch-commit-pr.cmd "feature-description" "commit message"

if "%~2"=="" (
    echo Usage: branch-commit-pr.cmd "feature-description" "commit message"
    echo Example: branch-commit-pr.cmd "fix claude cli error" "Fix Windows command line length limitation"
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0branch-commit-pr.ps1" "%~1" "%~2"