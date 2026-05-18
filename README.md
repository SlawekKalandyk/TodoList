# TodoList

TodoList is an Avalonia desktop todo app that runs as a tray-first right-side panel with local SQLite storage.

## Highlights

- .NET 8 Avalonia MVVM desktop app.
- Tray-first startup (panel starts hidden).
- Lazy startup allocation: window, repository, and main view model are created only when the panel is first opened.
- Tray icon actions:
  - Toggle Todos
  - Exit
- Borderless, topmost side panel snapped to the right edge of the current screen.
- Auto-hide on focus loss when not pinned.
- Dark theme default.

## Features

- Add todos with a priority: Minor, Normal (default), Major, Critical.
- Mark complete, delete, reject, and restore rejected items.
- Inline rename by Rename button (pen icon):
  - Enter or focus loss commits
  - Escape cancels
- Expand per-todo details panel to edit:
  - Priority
  - Due date (with clear action)
  - Notes
- Smart views:
  - None
  - Due today
  - Due soon (next 7 days)
  - Overdue
- Filters combine with AND semantics:
  - Status filter: Active, Completed, Rejected, All
  - Priority filter: All, Minor, Normal, Major, Critical
  - Search query over title, with optional notes search
  - Note: when Smart View is not None, status filter is disabled by design
- Grouping options:
  - None
  - Added day
  - Due date
  - Priority
- Ordering options:
  - Creation date
  - Due date
  - Priority
  - Direction: Descending or Ascending
- Pagination:
  - Page size options: 10, 25, 50, 100
  - Previous/next page navigation with visible-range status text
  - Flat mode pages directly from SQLite (`QueryCount` + `QueryPage`)
  - Grouped mode preserves group ordering first, then slices pages
  - Groups may span pages; each page segment still renders the group header so rows are not visually orphaned
- Summary counts for active, completed, and rejected.

## Panel Behavior

- Width control is stored as a percent where 100% = 600px.
- Allowed width percent range is 80-200 (480px-1200px).
- Pin toggle keeps the panel open when focus changes.
- Keyboard shortcuts:
  - Ctrl+F focuses search
  - Escape clears search when search is focused

## Tech Stack

- Avalonia 11.3.6
- CommunityToolkit.Mvvm 8.2.1
- Dapper 2.1.35
- Microsoft.Data.Sqlite 8.0.12

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
- Selected smart view
- Selected priority filter
- Selected grouping option
- Selected ordering option
- Selected ordering direction

Pagination state is not persisted yet (page size/current page reset per session).

## Project Structure

- `src/TodoList.App` - Avalonia application entry point and composition
- `src/TodoList.App/Data` - repository abstractions and SQLite implementation
- `src/TodoList.App/Models` - domain models, enums, and settings model
- `src/TodoList.App/ViewModels` - UI behavior, commands, filtering, grouping, ordering
- `src/TodoList.App/Views` - window XAML and interaction event handlers

## Current Notes

- There is currently no automated test project in this repository.
- Todo persistence is synchronous in the current implementation.