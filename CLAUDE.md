# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Canonical Agent Guide

[AGENTS.md](AGENTS.md) is the primary agent reference. It is kept current and contains the architecture map, repo-specific rules, storage/migration facts, and a change checklist. Read it first.

[README.md](README.md) describes user-facing behavior and features. [docs/feature-roadmap.md](docs/feature-roadmap.md), [docs/v1.1-implementation-checklist.md](docs/v1.1-implementation-checklist.md), and [docs/performance-improvements.md](docs/performance-improvements.md) carry product/release context.

## Stack & Layout

- Avalonia 11 desktop app, .NET 8 (`net8.0`), CommunityToolkit.Mvvm, Dapper + Microsoft.Data.Sqlite.
- Single project: [src/TodoList.App](src/TodoList.App) (MVVM split across `Views/`, `ViewModels/`, `Data/`, `Models/`).
- No test project exists in this repo.

## Common Commands

From repo root (PowerShell):

```powershell
dotnet restore TodoList.slnx
dotnet build TodoList.slnx
dotnet run --project src/TodoList.App/TodoList.App.csproj
```

If the app is running and build output is locked, redirect output:

```powershell
dotnet build src/TodoList.App/TodoList.App.csproj -p:OutDir=e:/Programming/personal/TodoList/artifacts/build-test/
```

Publishing via [Makefile](Makefile): `make help`, `make publish-single`, `make publish-framework` (overrides: `CONFIG`, `RID`, `PUBLISH_ROOT`).

## Architecture (Big Picture)

- App lifecycle, tray, lazy `MainWindow`/repository/VM creation, settings load + sanitize + legacy filter normalization, debounced settings persistence: [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs).
- Main UI markup and tap/dropdown stability guards: [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml) + [src/TodoList.App/Views/MainWindow.axaml.cs](src/TodoList.App/Views/MainWindow.axaml.cs).
- Smart views, filtering/search (200 ms debounce), grouping/ordering, pagination, command wiring: [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs); per-item edit state in [src/TodoList.App/ViewModels/TodoItemViewModel.cs](src/TodoList.App/ViewModels/TodoItemViewModel.cs).
- Persistence: [src/TodoList.App/Data/ITodoRepository.cs](src/TodoList.App/Data/ITodoRepository.cs) + [src/TodoList.App/Data/SqliteTodoRepository.cs](src/TodoList.App/Data/SqliteTodoRepository.cs) (synchronous; additive schema migration via `EnsureColumnExists`). UI settings in [src/TodoList.App/Data/JsonAppSettingsStore.cs](src/TodoList.App/Data/JsonAppSettingsStore.cs).
- Storage paths: `%LOCALAPPDATA%/TodoListPanel/{todos.sqlite,settings.json}` (Debug uses `TodoListPanel-debug`).

## Non-Obvious Rules (cross-file invariants)

These break silently if violated — see AGENTS.md for the full list and rationale.

- Grouping/ordering option strings in `MainWindowViewModel.cs` must stay aligned with `SanitizeSettings` arrays in `App.axaml.cs`; mismatches sanitize values back to defaults.
- Filtering uses AND across smart view, status, priority, and search. When `SelectedSmartView != None`, status filter is intentionally disabled.
- Width percent is anchored to base 600 px, range 80-200; window min/max must stay at 480/1200 across `MainWindow.axaml`, `MainWindow.axaml.cs`, and `App.axaml.cs` or right-edge snapping drifts on multi-monitor.
- `TodoFilter` retains legacy priority values for settings migration — update `NormalizeLegacyCombinedFilter` in `App.axaml.cs` when changing filter enums.
- Due dates are stored as nullable UTC unix seconds; conversion to local time happens in `TodoItemViewModel`.
- Inline rename uses in-place update + filter reapply (no full reload). Flat and grouped lists are `ScrollViewer` + `ItemsControl` — don't assume `ListBox` semantics in event handlers.
- Settings writes go through a 350 ms debounce that is force-flushed on close; keep new settings on that path.
