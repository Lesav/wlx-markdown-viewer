@echo off
setlocal

set "MARKDOWNVIEW_DIR=%~dp0"
set "WINDOWS_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

if not exist "%WINDOWS_POWERSHELL%" (
    echo [MarkdownView] Windows PowerShell was not found.
    echo [MarkdownView] Cannot remove Internet security blocks.
    set "RESULT=2"
    goto :finish
)

echo [MarkdownView] Removing Internet security blocks...
"%WINDOWS_POWERSHELL%" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; Get-ChildItem -LiteralPath $env:MARKDOWNVIEW_DIR -Recurse -File -Force | Unblock-File -ErrorAction Stop"

if errorlevel 1 (
    echo [MarkdownView] Failed. Run this file as administrator and try again.
    set "RESULT=1"
) else (
    echo [MarkdownView] Done. Restart Total Commander or Double Commander.
    set "RESULT=0"
)

:finish
if /i not "%~1"=="/quiet" pause
exit /b %RESULT%
