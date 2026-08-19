@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Never remove anything unless this script is located in a directory named MarkdownView.
for %%D in ("%~dp0.") do (
    set "SCRIPT_DIR=%%~fD"
    set "SCRIPT_DIR_NAME=%%~nxD"
)

if /I not "%SCRIPT_DIR_NAME%"=="MarkdownView" (
    echo ERROR: MarkdownView-drop.cmd must be located in a directory named MarkdownView.
    echo No files were changed.
    exit /b 1
)

cd /D "%SCRIPT_DIR%" || (
    echo ERROR: Cannot open "%SCRIPT_DIR%".
    exit /b 1
)

set "SELF=%~f0"
set "RESULT=0"

rem Delete every file below the plugin directory. A file that is still in use is
rem renamed by appending .drop so it can be removed after the locking process exits.
for /F "delims=" %%F in ('dir /A-D /B /S "%SCRIPT_DIR%\*" 2^>nul') do (
    if /I not "%%~fF"=="%SELF%" (
        del /F /Q "%%~fF" >nul 2>&1
        if exist "%%~fF" (
            if exist "%%~fF.drop" del /F /Q "%%~fF.drop" >nul 2>&1
            move /Y "%%~fF" "%%~fF.drop" >nul 2>&1
            if exist "%%~fF" (
                echo ERROR: Cannot delete or rename "%%~fF".
                set "RESULT=1"
            ) else (
                echo Renamed locked file: "%%~fF.drop"
            )
        )
    )
)

rem Remove directories that became empty, deepest paths first.
for /F "delims=" %%D in ('dir /AD /B /S "%SCRIPT_DIR%\*" 2^>nul ^| sort /R') do rd "%%~fD" >nul 2>&1

if "%RESULT%"=="0" (
    echo MarkdownView files were removed.
) else (
    echo Some files could not be removed or renamed.
)

rem Delete this script last. If deletion is blocked, append .drop to its name too.
(goto) 2>nul & del /F /Q "%SELF%" >nul 2>&1 & if exist "%SELF%" move /Y "%SELF%" "%SELF%.drop" >nul 2>&1
