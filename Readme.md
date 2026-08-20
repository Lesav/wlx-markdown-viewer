# Markdown Lister Plugin for Total Commander and Double Commander (32/64-bit)

Current release: **2.9.4**.

MarkdownView is based on the [wlx-markdown-viewer plugin](https://github.com/rg-software/wlx-markdown-viewer).
Markdown files are parsed with [Markdig](https://github.com/xoofx/markdig) and displayed as a WPF
`FlowDocument`.

The Markdown viewer targets .NET Framework 4.8, which is included with supported Windows 10
installations. It does not use WebView2 or Internet Explorer and does not execute JavaScript or
PHP. It does not require a separately installed .NET/.NET Core runtime. All non-system DLLs are
included in the plugin directory.

## Markdown rendering

The WPF renderer supports headings, paragraphs, emphasis, links, fenced code, lists, quotes,
tables, task lists, local images, selection, copy, Total Commander search and dark mode. Raw HTML
inside Markdown is disabled. Relative images are restricted to the directory containing the
Markdown file.

Task-list checkboxes are drawn as WPF vector controls instead of font glyphs, so both checked and
unchecked states remain visible in light and dark themes.

Fenced code blocks are syntax-highlighted locally without external libraries. Supported languages
and aliases are:

- JavaScript: `js`, `javascript`, `node`, `nodejs`;
- Bash/Shell: `bash`, `sh`, `shell`, `zsh`;
- SQL: `sql`, `mysql`, `postgres`, `postgresql`, `pgsql`, `sqlite`, `tsql`, `mssql`, `plsql`;
- CMD/Batch: `cmd`, `bat`, `batch`, `dos`;
- PowerShell: `powershell`, `ps1`, `pwsh`;
- markup: `html`, `htm`, `xhtml`, `xml`, `svg`.

Comments, strings, keywords, numbers, literals, variables, operators and PowerShell commands use
separate high-contrast colors in light and dark themes. Unknown languages and code blocks larger
than the highlighting safety limit remain readable as plain monospaced text.

Every regular code block has a button in its upper-right corner that copies the original code
without Markdown fences, a language name or syntax-highlighting markup. A check mark confirms a
successful copy; clipboard access is retried when another Windows application temporarily owns it.

Fenced `mermaid` blocks are rendered locally to SVG by the bundled .NET Framework 4.8 port of
[Mermaider](https://github.com/nullean/mermaider), then converted to WPF drawings by
[SharpVectors](https://github.com/ElinamLLC/SharpVectors). No browser engine or network access is
used for diagrams.
The adapted Mermaider sources are vendored in `ThirdParty\Mermaider` and built directly with the
plugin; they are not downloaded as an external source dependency during the build.

Configuration is stored in `MarkdownView.ini`:

- `Extensions: MarkdownExtensions` lists file extensions handled as Markdown.
- `Renderer: Extensions` selects Markdig extensions. The default enables common, advanced,
  emoji, mathematics and task-list parsing.

## Repository layout

- `Build\` contains installation-package source files: `MarkdownView.ini`, Total Commander
  metadata and `MarkdownView-drop.cmd`. The build script copies this directory into the package
  staging area.
- `Markdown\` contains the C++/CLI bridge between the native WLX plugin and the managed WPF
  renderer. It produces the architecture-specific `Markdown-x86.dll` and `Markdown-x64.dll`.
- `Markdown.Wpf\` contains the .NET Framework 4.8 Markdown viewer: Markdig parsing, WPF
  `FlowDocument` rendering, themes, search, selection, local images and Mermaid integration.
- `MarkdownView\` contains the native x86/x64 WLX plugin, Total Commander Lister API entry points,
  window and keyboard handling, and version resources.
- `ThirdParty\` contains modified third-party source code that must be kept in the repository.
  Currently it holds the .NET Framework 4.8 port of Mermaider and Sugiyama together with upstream
  provenance and license files. NuGet dependencies are not stored here.

## Setup

The combined archive contains `MarkdownView.wlx` for 32-bit Total Commander or Double Commander
and `MarkdownView.wlx64` for their 64-bit editions on Windows. The archive also contains
`TEST.md` for checking F3, Esc, search, themes and Markdown/Mermaid rendering after installation.

### Total Commander

Open the ZIP in Total Commander and confirm the plugin installation. Total Commander selects the
correct architecture automatically.

### Double Commander

For automatic installation, run Double Commander's internal command `cm_AddPlugin` and select
the installation ZIP. For manual installation, extract the archive, open
**Configuration → Options → Plugins → WLX → Add**, and select the architecture-matching file:

- `MarkdownView.wlx64` for 64-bit Double Commander;
- `MarkdownView.wlx` for 32-bit Double Commander.

The plugin exports a WLX detection string generated from `MarkdownExtensions`, so Double
Commander can associate all configured Markdown extensions automatically. Keep the `runtime`
directory beside the WLX files. This build is Windows-only because its renderer uses WPF and
.NET Framework 4.8.

`MarkdownView-drop.cmd` removes files from an installed plugin directory. For safety it runs only
from a directory named `MarkdownView`. Files that are still locked are renamed by appending
`.drop`.

## Building

Run `BuildMakeSetup.bat` to restore dependencies, build x86 and x64 and create
`dist\MarkdownView-<version>.zip`. Before signing, the build checks that `signtool.exe` and both
configured code-signing certificates are available. If they are found, every unsigned PE file is
Authenticode-signed; otherwise an unsigned package is created with a warning. The certificate
thumbprints, store and `signtool.exe` path can be overridden through the environment for CI. All
intermediate build outputs and package staging files are kept under `tmp\`.

The build requires Visual Studio 2022 or Build Tools with the C++ workload, the .NET Framework 4.8
targeting pack, a current .NET SDK for the SDK-style projects, and Internet access for NuGet
restore. End users do not need these build tools or an additional runtime.

## Testing

Open `TEST.md` with F3 in Total Commander or Double Commander to check Markdown rendering,
Quick View, task-list checkboxes, syntax highlighting, per-block code copying, theme contrast,
selection, search, local Mermaid diagrams and closing Lister with Esc.
