# TodoList

Avalonia desktop todo app designed as a right-side panel opened from the system tray, with SQLite local storage.

## Implemented MVP

- Avalonia MVVM desktop app (.NET 8).
- Tray icon with menu:
	- Toggle Todos
	- Exit
- Side panel window behavior:
	- Borderless
	- Always on top
	- Anchored to the right side of the current screen work area
	- Hides when it loses focus
- Theming:
	- Dark theme only
- Todo features:
	- Quick add textbox at the top
	- Enter-to-add and Add button
	- Todo list body with complete and delete actions
	- Reject action for items that no longer matter (excluded from todo/completed)
	- Filters: Active, Completed, Rejected, All
	- Optional grouping by day todos were added
	- Clear completed action
	- Summary counts for active, completed, and rejected
- SQLite persistence via `Microsoft.Data.Sqlite`.

## Project Structure

- `src/TodoList.App` - Avalonia application
- `src/TodoList.App/Data` - SQLite repository
- `src/TodoList.App/Models` - Todo models and filter enum
- `src/TodoList.App/ViewModels` - MVVM logic
- `src/TodoList.App/Views` - Panel window UI

## Run

From repository root:

```powershell
dotnet restore TodoList.slnx
dotnet run --project src/TodoList.App/TodoList.App.csproj
```

## Storage Location

The SQLite file is created at:

`%LOCALAPPDATA%/TodoListPanel/todos.sqlite`

## Notes for Linux Port

The core architecture is already portable because it uses Avalonia and SQLite. For Linux, the app should work with minor behavior differences in tray and topmost window handling depending on desktop environment and Wayland/X11 rules.