# Hay Viewer

A portable, single-file Windows JSON viewer and editor built with .NET 8 and WPF.

## Features

- **Tabbed interface** — open multiple JSON files simultaneously; each tab shows `*` when unsaved
- **Syntax highlighting** — keys, strings, numbers, booleans, and nulls each get distinct colors
- **Code folding** — collapse/expand `{}` and `[]` blocks in the editor
- **Tree view** — collapsible tree of the JSON structure; click a node to jump to it in the editor
- **Format / Minify** — pretty-print with 2-space, 4-space, or tab indent; or collapse to one line
- **Validate** — shows error with line and column; highlights the offending line
- **JSON-aware search** — search by keys, values, or both; case-sensitive and regex options; highlights all matches with a match counter; `Enter`/`F3` = next, `Shift+F3` = previous
- **Light and dark themes**
- **Recent files** menu
- **Drag-and-drop** — drop a `.json` file onto the window to open it
- **Multiple windows** — `File > New Window` (or toolbar button)
- **Portable settings** — `settings.json` lives next to the `.exe`; no registry writes, no install required

## Build

**Requirements:** .NET 8 SDK (`dotnet --version` should show `8.x.x` or later)

```
git clone https://github.com/tleaha/hayviewer.git
cd hayviewer
dotnet build
```

## Run tests

```
dotnet test tests/HayViewer.Tests/HayViewer.Tests.csproj
```

## Publish (portable single-file executable)

```
dotnet publish src/HayViewer/HayViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
```

The output is `publish/HayViewer.exe` — a single ~70 MB self-contained executable. Copy it (and optionally `THIRD-PARTY-NOTICES.txt`) anywhere and run it; no install required.

## Keyboard shortcuts

| Action | Shortcut |
|---|---|
| Open file | `Ctrl+O` |
| Save | `Ctrl+S` |
| Save As | `Ctrl+Shift+S` |
| Find | `Ctrl+F` |
| Format JSON | `Ctrl+Shift+F` |
| Minify JSON | `Ctrl+M` |
| New tab | `Ctrl+Shift+T` |
| Find next | `Enter` or `F3` (in search bar) |
| Find previous | `Shift+Enter` or `Shift+F3` |
| Close search | `Esc` |

## Package versions

| Package | Version |
|---|---|
| AvalonEdit (NuGet: `AvalonEdit`) | 6.3.1.120 |
| .NET target | net8.0-windows |
| xunit | 2.5.3 |
| Microsoft.NET.Test.Sdk | 17.8.0 |

## Known limitations

- The tree view is rebuilt on Format/Refresh only (not on every keystroke), to avoid blocking large-file edits.
- For files over ~20 MB, Format/Minify/Validate run synchronously; a progress indicator is not yet shown (the UI may briefly pause on very large files).
- AvalonEdit tab content is recreated when switching tabs; folding state is not preserved across tab switches.
- The exe targets `win-x64` only.

## Third-party notices

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for the AvalonEdit MIT license text.
