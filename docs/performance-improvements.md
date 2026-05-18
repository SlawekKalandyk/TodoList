# TodoList Performance Improvements

Date: 2026-05-17
Last updated: 2026-05-18
Status: In Progress
Scope: Desktop app at src/TodoList.App (Avalonia + MVVM + SQLite)

## 1) Purpose

This document tracks performance-focused improvements after list virtualization was added for flat and grouped rows.

Goals:
- Improve UI responsiveness for filtering, searching, and common row actions.
- Reduce avoidable UI-thread work and repeated collection churn.
- Reduce unnecessary disk writes and SQLite round trips.
- Keep behavior consistent with current MVVM boundaries.

## 2) Current Baseline

Implemented:
- Virtualized list rendering for flat and grouped displays.
- P1 complete: ApplyFilter rebuilds only the active visible branch; inactive branch is rebuilt only when grouping mode changes.
- P4 complete: due-date persistence now has a single authoritative save trigger, and due-date ordering/grouping refresh runs only in that path.
- P2 complete (pagination-adjusted): complete/reject/restore/delete now persist item changes, refresh summary counts via repository aggregate query, and re-filter without reloading all rows.
- P3 complete: visible flat/grouped collections now use incremental remove/move/insert synchronization instead of full clear + repopulate on each filter pass.
- P6 complete for flat mode and adapted for grouped pagination: filtering/counting and flat-page retrieval are SQL-backed; grouped pagination uses SQL-filtered rows with in-memory group-order slicing.
- P7 complete: indexes for status/due/priority filter and ordering paths are now created during schema ensure/migration.
- P5 complete: UI settings persistence requests are debounced and pending writes are flushed immediately on close/exit.

Current architecture notes:
- Filtering and ordering criteria are translated to SQLite query predicates/order clauses in the repository.
- MainWindowViewModel now orchestrates criteria + pagination and maps paged todo rows to existing in-memory item view models.
- SQLite schema ensure now creates additional indexes aligned to status/due/priority query paths used by `QueryPage` and `QueryCount`.
- Full `LoadTodos` usage is now focused on startup/add flows.
- Grouped pagination currently uses a hybrid path: fetch filtered rows from SQLite, apply group ordering in memory, then slice the page.
- Filter application now rebuilds only the active branch during normal updates.
- Visible collection updates use diff synchronization with grouped-row reuse by key to reduce notification churn.
- Settings persistence requests are debounced (350 ms) and force-flushed on window close/app exit.

## 3) Performance Targets

Use these as practical targets for local verification on a mid-range laptop.

- Search input response:
  - <= 16 ms median update time at 1,000 todos.
  - <= 60 ms median update time at 10,000 todos.
- Filter, grouping, and ordering switch:
  - <= 50 ms median at 1,000 todos.
- Single-item write actions (complete, reject, rename, due date, notes):
  - no full list reload unless required by correctness.
- Disk writes:
  - no duplicate writes from a single user action.

## 4) Highest-Value Improvements

| ID | Improvement | Impact | Effort | Risk |
| --- | --- | --- | --- | --- |
| P1 | Rebuild only the active list branch in ApplyFilter (done 2026-05-17) | High | Small | Low |
| P2 | Replace full LoadTodos reloads with in-place item updates (done 2026-05-17) | High | Medium | Medium |
| P3 | Replace Clear + Add loops with range or diff updates (done 2026-05-18) | High | Medium | Medium |
| P4 | Remove duplicate due-date save path (done 2026-05-17) | Medium | Small | Low |
| P5 | Debounce settings persistence writes (done 2026-05-18) | Medium | Small | Low |
| P6 | Push filtering and ordering into SQLite queries for scale (done 2026-05-18) | High | Large | Medium |
| P7 | Add indexes for due/status/priority heavy queries (done 2026-05-18) | Medium | Small | Low |
| P8 | Optional FTS5 for title + notes search | Medium to High | Medium | Medium |

## 5) Detailed Plan

### P1) Rebuild only active list branch

Status:
- Done on 2026-05-17.

Problem:
- ApplyFilter currently refreshes both VisibleTodos and VisibleGroupedRows on each pass, but only one branch is visible.

Approach:
- If ShowFlatList is true, refresh only VisibleTodos.
- If ShowGroupedList is true, refresh only VisibleGroupedRows.
- Rebuild the inactive branch only when grouping mode changes.

Primary touchpoint:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs

Expected gain:
- Less collection churn and fewer UI notifications during search and filter changes.

### P2) Remove full reloads after single-item mutations

Status:
- Done on 2026-05-17.

Problem:
- Actions like complete, reject, restore, and delete often call LoadTodos, which re-queries and recreates all view models.

Approach:
- Update affected item state in memory when possible.
- Refresh summary counts from repository aggregates after status-changing actions.
- Re-apply filter/order without full reload.

Implementation notes:
- `ToggleCompleted`, `DeleteTodo`, `RejectTodo`, and `RestoreTodo` now mutate in-memory state and call `ApplyFilter`.
- Summary counts are refreshed through `ITodoRepository.GetSummaryCounts()` after each status-changing mutation.
- `LoadTodos` remains for startup/add flow, not per-action recovery fallback.

Primary touchpoint:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs

Expected gain:
- Faster command response and less visible list jitter.

### P3) Range or diff updates for visible collections

Status:
- Done on 2026-05-18.

Problem:
- Clear + Add loops cause many notifications and repeated layout work.

Approach:
- Introduce a small range-aware collection helper or diff updater.
- Apply minimal edits between old and new result sets.

Implementation notes:
- `RefreshVisibleTodos` now applies incremental sync instead of `Clear` + `Add`.
- Grouped rows are first built into a desired list, reusing existing row view models by stable key when possible, then synchronized incrementally.
- Shared remove/move/insert synchronization helper is used for both flat and grouped visible collections.

Primary touchpoints:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- optional new utility collection class under src/TodoList.App

Expected gain:
- Smoother updates on large filtered result sets.

### P4) Remove duplicate due-date writes

Status:
- Done on 2026-05-17.

Problem:
- Due date update can be saved on selected date change and again on focus loss.

Approach:
- Keep one authoritative save trigger.
- Keep reorder refresh behavior only where necessary.

Implementation notes:
- DatePicker LostFocus save path was removed.
- Due-date save command now also refreshes filtered/grouped ordering only when due-date ordering or grouping is active.

Primary touchpoints:
- src/TodoList.App/Views/MainWindow.axaml.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs

Expected gain:
- Lower write volume and less repeated sort/filter work.

### P5) Debounce settings persistence

Status:
- Done on 2026-05-18.

Problem:
- Settings snapshot may be written frequently during rapid UI state changes.

Approach:
- Add a short debounce timer (250-500 ms) for settings save requests.
- Always flush immediately on app exit and window close.

Implementation notes:
- Added a 350 ms debounce timer in `App.axaml.cs` and routed property-change-triggered settings saves through a debounced request path.
- Added immediate flush path for pending settings writes on desktop exit and window close.
- Kept immediate save fallback when debounce timer is unavailable.

Primary touchpoints:
- src/TodoList.App/App.axaml.cs
- src/TodoList.App/Data/JsonAppSettingsStore.cs

Expected gain:
- Reduced disk I/O and less micro-stutter under rapid interactions.

### P6) Move filter/order work into SQLite

Status:
- Done on 2026-05-18.

Problem:
- Current model loads all rows and filters in memory each pass.

Approach:
- Add repository query methods accepting active filter criteria.
- Use SQL WHERE and ORDER BY for status, priority, smart view windows, and search.
- Keep view model as orchestration layer and grouping composer.

Implementation notes:
- Added `ITodoRepository.QueryPage(TodoQueryOptions, limit, offset)` and `QueryCount(TodoQueryOptions)` with SQLite query builders for smart view, status, priority, search, and ordering predicates.
- `MainWindowViewModel.ApplyFilter` now builds `TodoQueryOptions`, fetches total count + paged rows, and maps them to existing in-memory todo view models.
- Flat mode uses SQL ordering and paging directly.
- Grouped mode uses a hybrid flow for UX consistency: fetch filtered rows, apply group-order in the view model, then slice the current page.
- The older `QueryIds` pipeline was removed during cleanup.

Primary touchpoints:
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs

Expected gain:
- Better scalability for flat mode and reduced managed-memory pressure versus pre-pagination full-load filtering.

### P7) Add and validate indexes

Status:
- Done on 2026-05-18.

Problem:
- Not all common filter/order paths appear indexed.

Approach:
- Add indexes aligned to query patterns (status, due date, priority, created date).
- Validate with EXPLAIN QUERY PLAN during development.

Implementation notes:
- Added/ensured indexes for `(IsRejected, IsCompleted, DueAtUtc, CreatedAtUtc, Id)`, `(IsRejected, IsCompleted, Priority, CreatedAtUtc, Id)`, `(DueAtUtc, CreatedAtUtc, Id)`, and `(Priority, CreatedAtUtc, Id)`.
- Index creation now runs after column migration checks to stay safe for legacy databases that may be missing newer columns prior to migration.

Primary touchpoint:
- src/TodoList.App/Data/SqliteTodoRepository.cs

Expected gain:
- Faster query planning and reduced scan cost.

### P8) Optional FTS5 for search

Problem:
- Contains-based search over title and notes scales poorly.

Approach:
- Add FTS5 virtual table and sync triggers.
- Use MATCH queries for full-text search path.

Primary touchpoints:
- src/TodoList.App/Data/SqliteTodoRepository.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs

Expected gain:
- Significant search speed improvement for large note-heavy datasets.

## 6) Suggested Execution Order

1. P8 FTS5 (optional, after search scaling needs are confirmed).

## 7) Validation Checklist

After each improvement:
- Build: dotnet build TodoList.slnx
- Manual checks:
  - Search typing remains smooth while preserving current filter semantics.
  - Grouped and flat modes produce consistent visible rows.
  - Due-date edits persist once per user action.
  - No regressions in rename, details expansion, and tray reopen behavior.

## 8) Risks and Mitigations

- Risk: incremental updates can drift from source truth.
  - Mitigation: keep repository as source of truth, refresh summary aggregates from SQLite, and re-apply filter/paging after mutations.
- Risk: SQL query complexity can reduce maintainability.
  - Mitigation: centralize query builder logic and keep clear criteria mapping.
- Risk: index bloat can slow writes.
  - Mitigation: add only indexes tied to observed query patterns.
- Risk: grouped pagination hybrid path can become expensive at very large filtered counts.
  - Mitigation: keep this behavior explicit and consider dedicated grouped SQL pagination (or cursor-style grouping) if large grouped datasets become common.

## 9) Pagination Compatibility Summary

| ID | Status with Pagination | Notes |
| --- | --- | --- |
| P1 | Unchanged | Active-branch-only rebuild still applies. |
| P2 | Changed | Summary counts now come from repository aggregate query, not `_allTodos`. |
| P3 | Unchanged | Incremental visible collection sync remains active. |
| P4 | Unchanged | Single due-date save trigger still applies. |
| P5 | Unchanged | Debounced settings persistence still applies. |
| P6 | Changed | Flat mode is SQL count/page; grouped mode is hybrid SQL + in-memory group-order paging. |
| P7 | Unchanged | Index strategy still aligns with query paths. |
| P8 | Not implemented | Optional follow-up. |

## 10) Out of Scope

- Changing synchronous repository model to async.
- Major UI redesign.
- Non-performance feature expansion.
