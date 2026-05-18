# AGENTS.md

## Purpose
Use this file as the fast path for AI coding agents working in this repository.
For product-level behavior and release context, read [README.md](README.md), [docs/feature-roadmap.md](docs/feature-roadmap.md), [docs/v1.1-implementation-checklist.md](docs/v1.1-implementation-checklist.md), and [docs/performance-improvements.md](docs/performance-improvements.md).

## Project Snapshot
- Solution: [TodoList.slnx](TodoList.slnx)
- App project: [src/TodoList.App/TodoList.App.csproj](src/TodoList.App/TodoList.App.csproj)
- Stack: Avalonia Desktop + CommunityToolkit.Mvvm + SQLite
- Runtime target: .NET 8 (`net8.0`)
- Test status: no automated test project in this repository today
- Startup behavior: app starts hidden in tray mode; open/close panel via tray icon or tray menu
- Startup allocation: `MainWindow`, repository, and `MainWindowViewModel` are created lazily on first panel open in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs)

## Commands Agents Should Use
From repo root:

```powershell
dotnet restore TodoList.slnx
dotnet build TodoList.slnx
dotnet run --project src/TodoList.App/TodoList.App.csproj
```

`dotnet restore` is usually needed after cloning or when package references change.

If the app is currently running, build output can be locked. Use:

```powershell
dotnet build src/TodoList.App/TodoList.App.csproj -p:OutDir=e:/Programming/personal/TodoList/artifacts/build-test/
```

Publishing helpers from [Makefile](Makefile):

```powershell
make help
make restore
make build
make publish-single
make publish-framework
```

Use `CONFIG`, `RID`, and `PUBLISH_ROOT` overrides when needed.

## Architecture Map
- App lifecycle + tray + lazy app object creation + settings load/sanitize + legacy filter normalization + settings persistence wiring: [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs)
- Main UI markup: [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml)
- Window behavior (snap, scaling, search shortcut handlers, rename/tap event handlers): [src/TodoList.App/Views/MainWindow.axaml.cs](src/TodoList.App/Views/MainWindow.axaml.cs)
- Main feature logic and commands (smart views, filtering/search, grouping/ordering, grouped rows): [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs)
- Todo item editing state: [src/TodoList.App/ViewModels/TodoItemViewModel.cs](src/TodoList.App/ViewModels/TodoItemViewModel.cs)
- Persistence contracts and implementations: [src/TodoList.App/Data/ITodoRepository.cs](src/TodoList.App/Data/ITodoRepository.cs), [src/TodoList.App/Data/SqliteTodoRepository.cs](src/TodoList.App/Data/SqliteTodoRepository.cs)
- UI settings persistence: [src/TodoList.App/Data/JsonAppSettingsStore.cs](src/TodoList.App/Data/JsonAppSettingsStore.cs)
- Domain models/enums: [src/TodoList.App/Models](src/TodoList.App/Models)

## Repo-Specific Rules That Matter
- Keep MVVM boundaries: UI-only logic in Views, behavior/state in ViewModels, persistence in Data layer.
- Todo persistence is synchronous today; avoid introducing async patterns unless required by the change.
- Width percent behavior is tied to base width 600 and allowed range 80-200.
  - Keep these values aligned across [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml), [src/TodoList.App/Views/MainWindow.axaml.cs](src/TodoList.App/Views/MainWindow.axaml.cs), and [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs).
  - Keep window min/max aligned with this range (480/1200) or right-edge snapping can drift on multi-monitor setups.
- Filtering uses AND semantics across smart view, status (when enabled), priority, and search.
- Smart view behavior is intentional: when `SelectedSmartView != TodoSmartView.None`, status filter is disabled (`IsStatusFilterEnabled == false`).
- Current smart views (enum order) are `None`, `Today`, `Overdue`, and `DueSoon` in [src/TodoList.App/Models/TodoSmartView.cs](src/TodoList.App/Models/TodoSmartView.cs).
- UI display order is intentionally `None`, `Due today`, `Due soon (7 days)`, then `Overdue`.
- Grouping/ordering option strings in [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs) must stay aligned with validation in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs) (`SanitizeSettings`).
  - Update both the arrays in `App.axaml.cs` and the private option constants in `MainWindowViewModel.cs`; mismatches cause settings values to be sanitized back to defaults.
- [src/TodoList.App/Models/TodoFilter.cs](src/TodoList.App/Models/TodoFilter.cs) still contains legacy priority values for settings migration.
  - Preserve/update normalization in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs) (`NormalizeLegacyCombinedFilter`) when changing filter enums.
- Inline rename is pen-button-driven; avoid reloading the entire list during rename commit.
  - Use in-place updates + filter reapply pattern from [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs).
- Flat and grouped lists both use `ScrollViewer` + `ItemsControl` row containers in [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml); avoid ListBox-specific assumptions in event handling.
- Details panel stability relies on source/geometry guards in [src/TodoList.App/Views/MainWindow.axaml.cs](src/TodoList.App/Views/MainWindow.axaml.cs) (for example `IsTapWithinVisibleTodoDetailsPanel` and control-type checks); preserve this when changing tap/dropdown handling.
- Due date storage uses nullable UTC unix seconds in SQLite and is converted to local time in [src/TodoList.App/ViewModels/TodoItemViewModel.cs](src/TodoList.App/ViewModels/TodoItemViewModel.cs).
- Search filtering uses a 200 ms debounce in [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs); preserve debounce/perf behavior when changing search logic.

## Storage + Migration Facts
- Todo DB path: `%LOCALAPPDATA%/TodoListPanel/todos.sqlite`
- UI settings path: `%LOCALAPPDATA%/TodoListPanel/settings.json`
- Debug builds use `%LOCALAPPDATA%/TodoListPanel-debug/` for both files.
- Settings persistence is debounced (350 ms) and force-flushed on app exit/window close; keep new settings writes on the debounced path in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs).
- SQLite schema migration is additive in [src/TodoList.App/Data/SqliteTodoRepository.cs](src/TodoList.App/Data/SqliteTodoRepository.cs) via `EnsureColumnExists`.
- Legacy combined filter values from older settings are normalized in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs).
- Settings persistence includes selected status filter, smart view, priority filter, grouping option, ordering option, and ordering direction.
- For full storage and user-facing behavior details, prefer linking to [README.md](README.md) instead of copying sections.

## Change Checklist For Agents
When changing todo behavior, validate all touched layers:
1. Model/enum updates in [src/TodoList.App/Models](src/TodoList.App/Models)
2. Repository interface + SQLite implementation in [src/TodoList.App/Data](src/TodoList.App/Data)
3. Command/state wiring in [src/TodoList.App/ViewModels](src/TodoList.App/ViewModels)
4. XAML bindings and template visibility rules in [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml)
5. Settings load/save/sanitize and legacy filter normalization in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs) when smart views, filters, grouping, ordering, or search settings are affected
6. Build verification using one of the commands above
7. If publish flow changes, keep [Makefile](Makefile) targets aligned with README/agent guidance

## Planning Docs
- [README.md](README.md)
- [docs/feature-roadmap.md](docs/feature-roadmap.md)
- [docs/v1.1-implementation-checklist.md](docs/v1.1-implementation-checklist.md)
