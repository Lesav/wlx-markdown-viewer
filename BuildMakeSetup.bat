:: Build x86/x64 release binaries and create Total Commander installation packages.
@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "VERSION=2.9.1"
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "RESTORE_SOURCES=https://api.nuget.org/v3/index.json"

if not exist "%VSWHERE%" (
    echo ERROR: vswhere.exe was not found.
    echo Install Visual Studio 2022 or Build Tools with the C++ workload.
    exit /b 1
)

for /f "usebackq tokens=*" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSINSTALLDIR=%%I"

if not defined VSINSTALLDIR (
    echo ERROR: Visual Studio with MSBuild was not found.
    exit /b 1
)

set "MSBUILD=%VSINSTALLDIR%\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo ERROR: MSBuild was not found at "%MSBUILD%".
    exit /b 1
)

set "TMP_DIR=%~dp0tmp"
set "BUILD_DIR=%TMP_DIR%\build"
set "PACKAGE_DIR=%TMP_DIR%\package\%VERSION%\combined"
set "DIST_DIR=%~dp0dist"
set "ZIP_PATH=%DIST_DIR%\MarkdownView-%VERSION%.zip"
set "MANAGED_DIR=%BUILD_DIR%\managed\Release"

call :RemoveDirectory "%BUILD_DIR%"
if errorlevel 1 exit /b 1
call :RemoveDirectory "%TMP_DIR%\package\%VERSION%"
if errorlevel 1 exit /b 1
if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"
if errorlevel 1 exit /b 1
if exist "%ZIP_PATH%" del /F /Q "%ZIP_PATH%" 2>nul
if exist "%ZIP_PATH%" (
    echo ERROR: Failed to remove old package "%ZIP_PATH%".
    exit /b 1
)

mkdir "%PACKAGE_DIR%" >nul
xcopy "%~dp0Build\*" "%PACKAGE_DIR%\" /E /I /Y >nul
if errorlevel 1 (
    echo ERROR: Failed to copy package metadata.
    exit /b 1
)
mkdir "%PACKAGE_DIR%\doc" >nul
copy /Y "%~dp0Readme.md" "%PACKAGE_DIR%\doc\Readme.md" >nul
if errorlevel 1 exit /b 1
call :CopyRequired "%~dp0CANGELOG.md" "%PACKAGE_DIR%\CANGELOG.md"
if errorlevel 1 exit /b 1
call :CopyRequired "%~dp0TEST.md" "%PACKAGE_DIR%\TEST.md"
if errorlevel 1 exit /b 1

echo.
echo Building Release managed WPF renderer...
"%MSBUILD%" "%~dp0Markdown.Wpf\Markdown.Wpf.csproj" /nologo /verbosity:minimal /restore /t:Build /m "/p:Configuration=Release" "/p:Platform=AnyCPU" "/p:BaseOutputPath=%BUILD_DIR%\managed/" "/p:RestoreSources=%RESTORE_SOURCES%"
if errorlevel 1 (
    echo ERROR: Managed WPF renderer build failed.
    exit /b 1
)

call :BuildArchitecture Win32 x86 MarkdownView.wlx Markdown-x86.dll
if errorlevel 1 exit /b 1

call :BuildArchitecture x64 x64 MarkdownView.wlx64 Markdown-x64.dll
if errorlevel 1 exit /b 1

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0SignUnsignedPe.ps1" -Root "%PACKAGE_DIR%" -SignCommand "%~dp0sign.cmd"
if errorlevel 1 (
    echo ERROR: Failed to sign package PE files.
    exit /b 1
)

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::CreateFromDirectory($env:PACKAGE_DIR, $env:ZIP_PATH, [IO.Compression.CompressionLevel]::Optimal, $false)"
if errorlevel 1 (
    echo ERROR: Failed to create "%ZIP_PATH%".
    exit /b 1
)

echo.
echo Combined x86/x64 package created successfully:
echo   %ZIP_PATH%
exit /b 0

:BuildArchitecture
set "PLATFORM=%~1"
set "ARCH=%~2"
set "WLX_FILE=%~3"
set "MARKDOWN_DLL=%~4"
set "RUNTIME_DIR=%PACKAGE_DIR%\runtime\%ARCH%"
set "NATIVE_DIR=%BUILD_DIR%\native\bin\Release\%PLATFORM%"

echo.
echo Building Release %PLATFORM%...

"%MSBUILD%" "%~dp0MarkdownView.sln" /nologo /verbosity:minimal /restore /t:Build /m "/p:Configuration=Release;Platform=%PLATFORM%" "/p:RestoreSources=%RESTORE_SOURCES%"
if errorlevel 1 (
    echo ERROR: Release %PLATFORM% build failed.
    exit /b 1
)

mkdir "%RUNTIME_DIR%" >nul
call :CopyRequired "%NATIVE_DIR%\%WLX_FILE%" "%PACKAGE_DIR%\%WLX_FILE%"
if errorlevel 1 exit /b 1
call :CopyRequired "%NATIVE_DIR%\%MARKDOWN_DLL%" "%RUNTIME_DIR%\%MARKDOWN_DLL%"
if errorlevel 1 exit /b 1
for %%D in (
    Markdown.Wpf.dll
    Markdig.dll
    Mermaider.dll
    Sugiyama.dll
    SharpVectors.Converters.Wpf.dll
    SharpVectors.Core.dll
    SharpVectors.Css.dll
    SharpVectors.Dom.dll
    SharpVectors.Model.dll
    SharpVectors.Rendering.Wpf.dll
    SharpVectors.Runtime.Wpf.dll
    System.Buffers.dll
    System.Memory.dll
    System.Numerics.Vectors.dll
    System.Runtime.CompilerServices.Unsafe.dll
) do (
    call :CopyRequired "%MANAGED_DIR%\%%D" "%RUNTIME_DIR%\%%D"
    if errorlevel 1 exit /b 1
)

echo Added %ARCH% binaries to the combined package.
exit /b 0

:RemoveDirectory
if not exist "%~1" exit /b 0
for /L %%R in (1,1,5) do (
    rmdir /S /Q "%~1" 2>nul
    if not exist "%~1" exit /b 0
    powershell.exe -NoLogo -NoProfile -Command "Start-Sleep -Milliseconds 250"
)
echo ERROR: Failed to remove old directory "%~1".
exit /b 1

:CopyRequired
if not exist "%~1" (
    echo ERROR: Required build output is missing: "%~1".
    exit /b 1
)
copy /Y "%~1" "%~2" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy "%~1".
    exit /b 1
)
exit /b 0
