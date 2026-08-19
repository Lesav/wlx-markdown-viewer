# Changelog

## 2.9.1 - 2026-08-19

- Removed the optional HTML viewer, WebView2 SDK, legacy Internet Explorer/ActiveX host and all browser-specific source code.
- Removed HTML extensions, browser options, hotkeys and CSS settings from `MarkdownView.ini`.
- Removed `Build\css`, HTML resources and browser toolbar bitmaps from the installation package.
- Simplified the native WLX module to a Markdown-only host for the WPF `FlowDocument` renderer.
- Added dedicated `MarkdownView.def` and `MarkdownView.rc` source files for WLX exports and version metadata.
- Restored and adapted `TEST.md` as a viewer test document and included it in the installation ZIP.

## 2.9.0 - 2026-08-19

- Replaced the Markdown WebView renderer with a native WPF `FlowDocument` viewer targeting .NET Framework 4.8.
- Added an architecture-specific C++/CLI bridge between the native WLX module and the managed WPF renderer.
- Removed the optional HTML viewer, WebView2 SDK, legacy Internet Explorer/ActiveX host, browser settings and CSS assets; the plugin now handles Markdown only.
- Removed the separately installed .NET/.NET Core runtime requirement for end users.
- Added WPF rendering for headings, paragraphs, emphasis, links, fenced code, lists, quotes, tables, task lists and restricted local images.
- Added selection, copy, zoom, refresh and Total Commander Lister search support to the WPF viewer.
- Added local Mermaid-to-SVG rendering through the vendored .NET Framework 4.8 port of Mermaider, Sugiyama and SharpVectors.
- Recorded the Mermaider upstream commit and MIT license in `ThirdParty\Mermaider`; no external Mermaider source download is required.
- Increased dark-theme contrast and synchronized the viewer background with the document palette on every file load.
- Forwarded `Esc` from the WPF child window so Total Commander closes Lister normally.
- Bundled all managed dependencies in the plugin directory for both x86 and x64.
- Moved all build outputs, intermediate files and package staging into `tmp\`; release ZIP files now accumulate in `dist\`.
- Added repository-wide MSBuild output routing and removed obsolete root `bin`, `obj`, `Debug`, `Release` and `ReleaseWLX` artifacts.
- Removed the unused `Markdig.net` wrapper project and obsolete Internet Explorer registry archive from the active source tree.
- Documented the repository directory layout and the distinction between vendored sources and NuGet dependencies.

## 2.8.4 - 2026-08-19

- Added `MarkdownView-drop.cmd` to safely remove the installed plugin files.
- Locked files are renamed by appending `.drop`; the cleanup refuses to run unless its directory is named `MarkdownView`.
- Kept cleanup explicit because the WLX installation archive format has no pre-extraction command hook.

## 2.8.3 - 2026-08-19

- Made a missing WebView2 Runtime fall back to Internet Explorer silently by default.
- Clarified that the Edge browser and WebView2 Runtime are separate components.
- Added a diagnostic message when the required Microsoft .NET 8 Runtime is missing.
- Documented online and offline WebView2 Runtime installation options.
- Added automatic Authenticode signing of every unsigned PE file before packaging.
- Verified existing signatures and added dual SHA-1/SHA-256 signatures to project PE files before ZIP creation.

## 2.8.2 - 2026-08-19

- Restored `Esc` and other Total Commander Lister hotkeys when keyboard focus is inside WebView2.
- Forwarded keyboard messages from the WebView2 child window back to the WLX host.

## 2.8.1 - 2026-08-19

- Fixed blank F3 viewer windows by returning the WLX window immediately and completing WebView2 initialization asynchronously.
- Kept the viewer responsive while WebView2 creates its environment and controller.

## 2.8.0 - 2026-08-18

- Added Microsoft Edge WebView2 as the preferred rendering engine on Windows 10.
- Added automatic fallback to the restricted Internet Explorer engine when WebView2 is unavailable.
- Added browser engine selection and fallback controls to `MarkdownView.ini`.
- Disabled raw HTML in Markdown and hardened both rendering engines against scripts, ActiveX, Java, downloads and network resources.
- Added a combined Total Commander installation package containing both x86 and x64 WLX binaries.
- Updated `BuildMakeSetup.bat` to restore dependencies, build both architectures and create the installation ZIP.
- Synchronized file, product and package versions.
