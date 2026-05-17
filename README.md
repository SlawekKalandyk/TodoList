# TodoList

TodoList is an Avalonia desktop todo app that runs as a tray-first right-side panel with local SQLite storage.

## Highlights

- .NET 8 Avalonia MVVM desktop app.
- Tray-first startup (panel starts hidden).
- Tray icon actions:
  - Toggle Todos
  - Exit
- Borderless, topmost side panel snapped to the right edge of the current screen.
- Auto-hide on focus loss when not pinned.
- Dark theme default.

## Features

- Add todos with a priority: Minor, Normal (default), Major, Critical.
- Mark complete, delete, reject, and restore rejected items.
- Inline rename by double-click:
  - Enter or focus loss commits
  - Escape cancels
- Expand per-todo details panel to edit:
  - Priority
  - Due date (with clear action)
  - Notes
- Filters combine with AND semantics:
  - Status filter: Active, Completed, Rejected, All
  - Priority filter: All, Minor, Normal, Major, Critical
- Grouping options:
  - None
  - Added day
  - Due date
  - Priority
- Ordering options:
  - None
  - Due date
  - Direction: Descending or Ascending
- Summary counts for active, completed, and rejected.

## Panel Behavior

- Width control is stored as a percent where 100% = 600px.
- Allowed width percent range is 80-200 (480px-1200px).
- Pin toggle keeps the panel open when focus changes.

## Tech Stack

- Avalonia 11
- CommunityToolkit.Mvvm
- Dapper
- Microsoft.Data.Sqlite

## Getting Started

From repository root:

```powershell
dotnet restore TodoList.slnx
dotnet build TodoList.slnx
dotnet run --project src/TodoList.App/TodoList.App.csproj
```

If the app is already running and build output gets locked, use a custom output directory:

```powershell
dotnet build src/TodoList.App/TodoList.App.csproj -p:OutDir=e:/Programming/personal/TodoList/artifacts/build-test/
```

## Storage

- Todo database: `%LOCALAPPDATA%/TodoListPanel/todos.sqlite`
- UI settings: `%LOCALAPPDATA%/TodoListPanel/settings.json`

SQLite schema migrations are additive (missing columns are added when needed).

## Persisted UI Settings

- Panel width percent
- Pin state
- Selected status filter
- Selected priority filter
- Selected grouping option
- Selected ordering option
- Selected ordering direction

## Project Structure

- `src/TodoList.App` - Avalonia application entry point and composition
- `src/TodoList.App/Data` - repository abstractions and SQLite implementation
- `src/TodoList.App/Models` - domain models, enums, and settings model
- `src/TodoList.App/ViewModels` - UI behavior, commands, filtering, grouping, ordering
- `src/TodoList.App/Views` - window XAML and interaction event handlers

## Current Notes

- There is currently no automated test project in this repository.
- Todo persistence is synchronous in the current implementation.