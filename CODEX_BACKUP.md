# Codex Backup Context

Read this file first when continuing work on this repository in a fresh chat.

## Project

SPTextureMerger is a Windows-only WPF/.NET 8 desktop app. It merges maps exported from multiple Adobe Substance 3D Painter Texture Sets into one UV set.

The app is English-only. Keep UI strings and user docs in English.

## Current State

- Solution: `SPTextureMerger.sln`
- WPF app: `src/SPTextureMerger`
- Core merge library: `src/SPTextureMerger.Core`
- Lightweight test runner: `tests/SPTextureMerger.Tests`
- Published builds:
  - `publish/win-x64/SPTextureMerger.exe`
  - `publish/win-x64-self-contained/SPTextureMerger.exe`

The current UI is the glassmorphism version. Old dark UI publish folders were deleted. Do not reintroduce separate `*-glass` or `*-english` publish directories unless explicitly asked.

## Important Product Decisions

- Row = one source Texture Set.
- Column = one output map slot plus merge behavior.
- Each row needs a mask guide: transparent background, white UV island region or closed white UV island outline.
- First valid mask defines output dimensions.
- All masks and textures must match that size. No automatic resizing.
- Later rows override earlier rows where masks overlap.
- Output names are `{OutputBaseName}_{ColumnName}.png` or `.tiff`.
- Existing output files are overwritten without a confirmation dialog.
- User feedback uses in-app toast notifications. Avoid `MessageBox.Show` for normal app feedback because Windows alert sounds are unwanted.

## Merge Behaviors

- `RGBA Copy`: blend/copy full RGBA by mask coverage.
- `Normal Replace Normalize`: blend/copy normal RGB by mask coverage and renormalize; default uncovered pixels are `(128,128,255,255)`.
- `Data Copy`: raw channel copy for data maps.

## UI Notes

- WPF styling lives mainly in `src/SPTextureMerger/App.xaml`.
- Main window layout and dynamic grid are in `src/SPTextureMerger/MainWindow.xaml` and `.xaml.cs`.
- The UI should stay compact and tool-like, not a landing page.
- Do not show validation errors on startup.
- Validate on `Merge`.
- Output name typing must stay responsive; do not rebuild the grid on every `TextChanged`.
- Texture slots should not show full file paths on hover.
- Every texture slot has `Browse`, `Clear`, and `Preview`.
- Sort-order explanation belongs only in themed tooltips on the row arrow buttons.

## Substance 3D Painter Mask Workflow

The repo has `InSPTutorial`, a Chinese source note explaining the Painter workflow. The README already rewrites it in English.

Terminology should follow Adobe docs:

- `Texture Set`
- `Texture Set Settings`
- `Channels`
- `User Channel` / `User0`
- `Layer Stack`
- `Fill Layer`
- `Output Templates`
- `Input maps`
- `RGB+A`
- `Dilation + transparent`

## Commands

Build:

```powershell
dotnet build SPTextureMerger.sln -m:1 -v:m
```

Run tests:

```powershell
dotnet run --project tests/SPTextureMerger.Tests/SPTextureMerger.Tests.csproj
```

Publish framework-dependent:

```powershell
dotnet publish src/SPTextureMerger/SPTextureMerger.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
```

Publish self-contained:

```powershell
dotnet publish src/SPTextureMerger/SPTextureMerger.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64-self-contained
```

When publishing both variants, run the commands sequentially. Parallel publish can lock shared Release intermediates.

## Verification Expectations

Before handing work back:

1. Run `dotnet build SPTextureMerger.sln -m:1 -v:m`.
2. Run the test project.
3. If publishing changed, smoke-test both EXEs by starting them briefly and confirming the process stays alive.
4. If UI strings changed, search source docs for unintended Chinese text unless the user specifically asked to keep it.
