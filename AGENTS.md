# AGENTS.md

## Purpose
Use this file as the fast path for AI coding agents working in this repository.
For product-level context, read [README.md](README.md).

## Project Snapshot
- Solution: [TodoList.slnx](TodoList.slnx)
- App project: [src/TodoList.App/TodoList.App.csproj](src/TodoList.App/TodoList.App.csproj)
- Stack: Avalonia Desktop + CommunityToolkit.Mvvm + SQLite
- Runtime target: .NET 8 (`net8.0`)

## Commands Agents Should Use
From repo root:

```powershell
dotnet restore TodoList.slnx
dotnet build TodoList.slnx
dotnet run --project src/TodoList.App/TodoList.App.csproj
```

If the app is currently running, build output can be locked. Use:

```powershell
dotnet build src/TodoList.App/TodoList.App.csproj -p:OutDir=e:/Programming/personal/TodoList/artifacts/build-test/
```

## Architecture Map
- App lifecycle + tray + settings wiring: [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs)
- Main UI markup: [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml)
- Window behavior (snap, scaling, rename event handlers): [src/TodoList.App/Views/MainWindow.axaml.cs](src/TodoList.App/Views/MainWindow.axaml.cs)
- Main feature logic and commands: [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs)
- Todo item editing state: [src/TodoList.App/ViewModels/TodoItemViewModel.cs](src/TodoList.App/ViewModels/TodoItemViewModel.cs)
- Persistence contracts and implementations: [src/TodoList.App/Data/ITodoRepository.cs](src/TodoList.App/Data/ITodoRepository.cs), [src/TodoList.App/Data/SqliteTodoRepository.cs](src/TodoList.App/Data/SqliteTodoRepository.cs)
- UI settings persistence: [src/TodoList.App/Data/JsonAppSettingsStore.cs](src/TodoList.App/Data/JsonAppSettingsStore.cs)
- Domain models/enums: [src/TodoList.App/Models](src/TodoList.App/Models)

## Repo-Specific Rules That Matter
- Keep MVVM boundaries: UI-only logic in Views, behavior/state in ViewModels, persistence in Data layer.
- Todo persistence is synchronous today; avoid introducing async patterns unless required by the change.
- Width percent behavior is tied to base width 600 and allowed range 50-200.
  - Keep window min/max aligned with this range (300/1200) or right-edge snapping can drift on multi-monitor setups.
- Filtering is two-dimensional (status + priority) and uses AND semantics.
- Inline rename is double-click-driven; avoid reloading the entire list during rename commit.
  - Use in-place updates + filter reapply pattern from [src/TodoList.App/ViewModels/MainWindowViewModel.cs](src/TodoList.App/ViewModels/MainWindowViewModel.cs).

## Storage + Migration Facts
- Todo DB path: `%LOCALAPPDATA%/TodoListPanel/todos.sqlite`
- UI settings path: `%LOCALAPPDATA%/TodoListPanel/settings.json`
- SQLite schema migration is additive in [src/TodoList.App/Data/SqliteTodoRepository.cs](src/TodoList.App/Data/SqliteTodoRepository.cs) via `EnsureColumnExists`.

## Change Checklist For Agents
When changing todo behavior, validate all touched layers:
1. Model/enum updates in [src/TodoList.App/Models](src/TodoList.App/Models)
2. Repository interface + SQLite implementation in [src/TodoList.App/Data](src/TodoList.App/Data)
3. Command/state wiring in [src/TodoList.App/ViewModels](src/TodoList.App/ViewModels)
4. XAML bindings and template visibility rules in [src/TodoList.App/Views/MainWindow.axaml](src/TodoList.App/Views/MainWindow.axaml)
5. Settings load/save/sanitize in [src/TodoList.App/App.axaml.cs](src/TodoList.App/App.axaml.cs) when UI prefs are affected
6. Build verification using one of the commands above
