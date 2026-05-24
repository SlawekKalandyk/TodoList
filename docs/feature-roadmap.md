# TodoList Feature Roadmap

Date: 2026-05-17
Status: Draft for implementation
Scope: Desktop application at src/TodoList.App (Avalonia + MVVM + SQLite)

## 1) Purpose

This document turns the proposed feature list into an execution-ready roadmap.
It prioritizes by user impact, implementation effort, risk, and architectural fit.
It also defines acceptance criteria, estimated effort, and release boundaries.

## 2) Prioritized Feature Order (Best-First)

1. Smart views (Inbox, Today, Overdue, Upcoming, Recently Completed)
2. Full-text search (title + notes)
3. Undo for destructive actions (delete, reject, clear due date)
4. Bulk actions with multi-select and keyboard support
5. Reminders with desktop notifications and snooze
6. Recurring tasks (daily, weekly, monthly, custom interval)
7. Tags and tag filters
8. Import/export backup (JSON + CSV)
9. Archive + retention rules
10. Subtasks/checklists per todo

## 3) Prioritization Method

Each feature was scored on:
- User impact: How much daily friction it removes.
- Effort: Approximate development and verification cost.
- Risk: Data migration, state bugs, and UX complexity.
- Dependency fit: Whether later features depend on it.

Why this order:
- v1.1 focuses on immediate clarity and safety with minimal schema risk.
- v1.2 introduces planning automation (reminders and recurrence).
- v1.3 expands scale and portability features that benefit larger data sets.

## 4) Milestone Plan

Detailed execution checklist for this milestone:
- [v1.1 implementation checklist](v1.1-implementation-checklist.md)

| Milestone | Theme | Included Features | Estimated Duration |
| --- | --- | --- | --- |
| v1.1 | Focus and Safety | 1, 2, 3, 4 | 3-4 weeks |
| v1.2 | Proactive Planning | 5, 6, 7 | 4-5 weeks |
| v1.3 | Scale and Portability | 8, 9, 10 | 4-6 weeks |

Estimate basis:
- Single primary developer + manual QA.
- No automated test project currently in repository.
- Includes implementation, refinement, migration checks, and release fixes.

## 5) Detailed Scope by Milestone

## v1.1 - Focus and Safety

### Feature 1: Smart Views

User outcome:
- Users see what matters now without manually combining filters every session.

MVP scope:
- Add smart views: Inbox, Today, Overdue, Upcoming (7 days), Recently Completed (7 days).
- Smart view selection composes with existing status/priority filters using AND semantics.
- Persist selected smart view in app settings.

Out of scope:
- Saved custom views.
- Advanced date windows beyond upcoming 7 days.

Desktop UX requirements:
- Keyboard access to smart view picker.
- Clear visible selected state.
- No layout break at width 80-200 percent.

Likely implementation touchpoints:
- src/TodoList.App/Models/AppUiSettings.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/App.axaml.cs
- src/TodoList.App/Views/MainWindow.axaml

Acceptance criteria:
- Today shows active todos due on local current date.
- Overdue shows active todos with due date before local current date.
- Upcoming shows active todos due in the next 7 local days.
- Inbox shows active todos with no due date.
- Recently Completed shows completed and non-rejected todos with CompletedAtUtc within last 7 days.
- Selection survives app restart.

Estimate:
- 2.5 to 3.5 dev days.

Dependencies:
- Uses existing DueAtUtc and CompletedAtUtc.

Risks and mitigations:
- Risk: local date boundary confusion around midnight.
- Mitigation: centralize date-window calculations and test around day transitions.

### Feature 2: Full-Text Search (Title + Notes)

User outcome:
- Faster retrieval in larger lists.

MVP scope:
- Add search box with instant filter behavior.
- Match title and notes, case-insensitive.
- Persist optional last query in settings only if desired (recommended off by default).

Out of scope:
- Regex search.
- Highlight snippets and advanced query syntax.

Desktop UX requirements:
- Ctrl+F focuses search box.
- Escape clears search and restores full list.
- Clear button with keyboard focus support.

Likely implementation touchpoints:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Views/MainWindow.axaml.cs
- src/TodoList.App/Models/AppUiSettings.cs (if persistence added)
- src/TodoList.App/App.axaml.cs (if persistence added)

Acceptance criteria:
- Query filters across both title and notes.
- Filtering composes with status, priority, grouping, ordering, and smart views.
- UI remains responsive while typing with 1000 todos.

Estimate:
- 2 to 3 dev days.

Dependencies:
- None.

Risks and mitigations:
- Risk: repeated filtering can feel laggy.
- Mitigation: apply lightweight debounce (100-200 ms) and avoid unnecessary list rebuilds.

### Feature 3: Undo for Destructive Actions

User outcome:
- Recovery from accidental destructive actions without fear.

MVP scope:
- Undo for delete, reject, and clear due date.
- Single-step undo initially.
- Snackbar/inline notification with Undo action.

Out of scope:
- Multi-step timeline history.

Desktop UX requirements:
- Undo action keyboard reachable.
- Timeout visible and predictable.
- Focus returns to prior control after undo action completes.

Likely implementation touchpoints:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Views/MainWindow.axaml.cs
- src/TodoList.App/Data/ITodoRepository.cs (if restore by payload requires upsert)
- src/TodoList.App/Data/SqliteTodoRepository.cs (if upsert helper needed)

Acceptance criteria:
- Delete can be undone and item reappears with title, notes, priority, due date, and state.
- Reject can be undone and prior completion state is restored when applicable.
- Clear due date can be undone and prior due date restored.
- Undo prompt auto-dismisses after configured interval (for example 6 seconds).

Estimate:
- 2.5 to 4 dev days.

Dependencies:
- None, but easier if repository supports add-with-id or recovery insert path.

Risks and mitigations:
- Risk: undo payload drift if item mutates while undo is available.
- Mitigation: snapshot command payload at action time.

### Feature 4: Bulk Actions + Multi-Select

User outcome:
- Large-list cleanup and triage become significantly faster.

MVP scope:
- Multi-select mode for flat and grouped views.
- Bulk actions: Complete, Reject, Restore, Delete.
- Selection count indicator and clear selection action.

Out of scope:
- Drag box selection.
- Cross-window shared selection.

Desktop UX requirements:
- Keyboard selection support: Ctrl+A, Shift range select, Space toggle.
- Predictable focus after bulk command.
- Confirmation step for bulk delete.

Likely implementation touchpoints:
- src/TodoList.App/ViewModels/TodoItemViewModel.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Views/MainWindow.axaml.cs
- src/TodoList.App/Data/ITodoRepository.cs (optional batch APIs)
- src/TodoList.App/Data/SqliteTodoRepository.cs (optional batch APIs)

Acceptance criteria:
- Selection works in both grouped and ungrouped modes.
- Bulk operations apply to all selected items.
- Selection clears or updates consistently after operation.
- Bulk delete requires explicit confirmation.

Estimate:
- 4 to 5 dev days.

Dependencies:
- None.

Risks and mitigations:
- Risk: row interaction conflicts with existing details/rename gestures.
- Mitigation: define explicit interaction states (normal vs selection mode).

v1.1 total estimate:
- 11 to 15 dev days.

## v1.2 - Proactive Planning

### Feature 5: Reminders with Notifications and Snooze

User outcome:
- Important tasks are surfaced at the right time without manual checking.

MVP scope:
- Optional reminder timestamp per todo.
- Trigger desktop notification while app process is running.
- Snooze options: 10 min, 1 hour, tomorrow.

Out of scope:
- System-level scheduled notifications when app is not running.
- Email or external channel reminders.

Desktop UX requirements:
- Notification click returns focus to app panel and selects todo.
- Snooze actions are keyboard accessible.
- Respect pinned/hidden panel states.

Likely implementation touchpoints:
- src/TodoList.App/Models/TodoItem.cs
- src/TodoList.App/ViewModels/TodoItemViewModel.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs
- src/TodoList.App/App.axaml.cs

Data changes:
- Add ReminderAtUtc nullable column.
- Add ReminderSnoozedUntilUtc nullable column.
- Add ReminderDismissedAtUtc nullable column (optional).

Acceptance criteria:
- Reminder fires once when due if not completed/rejected.
- Snooze shifts next reminder trigger according to selected duration.
- Completed or rejected items do not trigger reminders.

Estimate:
- 4.5 to 6 dev days.

Dependencies:
- Requires reliable local time conversion and app timer loop.

Risks and mitigations:
- Risk: notification duplication across resume or restart.
- Mitigation: persist last-fired marker and idempotent trigger checks.

### Feature 6: Recurring Tasks

User outcome:
- Routine work regenerates automatically.

MVP scope:
- Recurrence patterns: Daily, Weekly, Monthly, Every N days.
- Completing a recurring todo creates next occurrence.
- Next occurrence copies title, notes, priority, tags (if implemented), and recurrence settings.

Out of scope:
- Complex rules like business days only, nth weekday, exclusion dates.

Desktop UX requirements:
- Recurrence controls in details panel remain compact at minimum width.
- Clear recurrence summary text visible in row/details.

Likely implementation touchpoints:
- src/TodoList.App/Models/TodoItem.cs
- src/TodoList.App/ViewModels/TodoItemViewModel.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs

Data changes:
- Add RecurrenceType (int) default none.
- Add RecurrenceInterval (int) nullable.
- Add RecurrenceAnchorUtc (int) nullable.
- Optionally add SourceRecurringTodoId for traceability.

Acceptance criteria:
- Completing recurring item creates exactly one next occurrence.
- Next due date follows selected recurrence rule in local time.
- Non-recurring items unchanged.

Estimate:
- 6 to 8 dev days.

Dependencies:
- Best paired with reminders for maximum value.

Risks and mitigations:
- Risk: duplicate next-occurrence creation on repeated toggles.
- Mitigation: enforce idempotent generation with recurrence instance checks.

### Feature 7: Tags and Tag Filters

User outcome:
- Better organization across projects and contexts than priority alone.

MVP scope:
- Add tag assignment in details panel.
- Add tag filter picker with AND semantics by default.
- Support quick-add tag input with suggestions from existing tags.

Out of scope:
- Tag colors and custom iconography.

Desktop UX requirements:
- Tag chips readable at high DPI.
- Keyboard create/remove tags from details panel.

Likely implementation touchpoints:
- src/TodoList.App/ViewModels/TodoItemViewModel.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs
- src/TodoList.App/App.axaml.cs (persist selected tag filters)
- src/TodoList.App/Models/AppUiSettings.cs (persist selected tag filters)

Data changes:
- Create Tags table: Id, Name unique.
- Create TodoTags join table: TodoId, TagId.
- Indexes on Name and TodoId.

Acceptance criteria:
- Users can add and remove tags from a todo.
- Filtering by one or more tags works with existing filters and search.
- Deleting a todo removes its join records.

Estimate:
- 4 to 5 dev days.

Dependencies:
- None.

Risks and mitigations:
- Risk: duplicate tag names with casing differences.
- Mitigation: normalized case-insensitive uniqueness.

v1.2 total estimate:
- 14.5 to 19 dev days.

## v1.3 - Scale and Portability

### Feature 8: Import/Export (JSON + CSV)

User outcome:
- Data portability, backup, and migration confidence.

MVP scope:
- Export all todos to JSON (full fidelity).
- Export flat CSV for spreadsheet workflows.
- Import JSON with preview count and conflict policy.

Out of scope:
- Encrypted exports.
- Cloud sync.

Desktop UX requirements:
- Native file picker usage.
- Clear success/failure feedback and partial failure report.

Likely implementation touchpoints:
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Views/MainWindow.axaml.cs
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs
- new helper files in src/TodoList.App/Data for serializer/import logic

Acceptance criteria:
- JSON export/import round-trips todos with due date, notes, priority, completion, rejection, and recurrence fields.
- CSV export includes practical columns for spreadsheet usage.
- Import reports created/updated/skipped counts.

Estimate:
- 3.5 to 5 dev days.

Dependencies:
- If tags/subtasks exist, include those in JSON schema versioning.

Risks and mitigations:
- Risk: timezone drift on imported dates.
- Mitigation: store UTC in export and display local conversion notes.

### Feature 9: Archive and Retention Rules

User outcome:
- Active list stays focused while preserving historical records.

MVP scope:
- Archive state distinct from rejected/completed.
- Toggle between active and archived views.
- Retention option: auto-archive completed items older than N days.

Out of scope:
- Multi-level archive folders.

Desktop UX requirements:
- Archive visibility clearly indicated.
- Archive actions require explicit labels to avoid confusion with delete.

Likely implementation touchpoints:
- src/TodoList.App/Models/TodoItem.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs
- src/TodoList.App/Models/AppUiSettings.cs
- src/TodoList.App/App.axaml.cs

Data changes:
- Add IsArchived int default 0.
- Add ArchivedAtUtc nullable.

Acceptance criteria:
- Archived items are hidden from default active views.
- Users can archive/unarchive without data loss.
- Auto-archive respects configured retention days.

Estimate:
- 3.5 to 5 dev days.

Dependencies:
- Works best after smart views/search.

Risks and mitigations:
- Risk: archive state conflicts with reject/completed semantics.
- Mitigation: define state precedence in one shared policy function.

### Feature 10: Subtasks / Checklists

User outcome:
- Complex todos become manageable through clear next steps.

MVP scope:
- Add checklist items inside todo details.
- Track subtask completion and show progress (for example 3/5).
- Optional setting: complete parent when all subtasks are complete (default off).

Out of scope:
- Nested subtasks.
- Dependencies between subtasks.

Desktop UX requirements:
- Fast keyboard creation (Enter creates next subtask).
- Reorder with keyboard shortcuts or simple move buttons.
- Detail panel remains usable at minimum width.

Likely implementation touchpoints:
- src/TodoList.App/Models/TodoItem.cs
- src/TodoList.App/ViewModels/TodoItemViewModel.cs
- src/TodoList.App/ViewModels/MainWindowViewModel.cs
- src/TodoList.App/Views/MainWindow.axaml
- src/TodoList.App/Data/ITodoRepository.cs
- src/TodoList.App/Data/SqliteTodoRepository.cs

Data changes:
- Create TodoSubtasks table: Id, TodoId, Title, IsCompleted, SortOrder, CreatedAtUtc.
- Index on TodoId and SortOrder.

Acceptance criteria:
- Users can add/edit/delete subtasks.
- Parent progress updates immediately.
- Subtasks persist and reload reliably.

Estimate:
- 7 to 9 dev days.

Dependencies:
- None.

Risks and mitigations:
- Risk: interaction density makes details panel crowded.
- Mitigation: collapsible subtasks section and strict spacing hierarchy.

v1.3 total estimate:
- 14 to 19 dev days.

## 6) Cross-Cutting Implementation Guidance

Architecture constraints to preserve:
- Keep MVVM boundaries: view interaction in Views, state/commands in ViewModels, persistence in Data.
- Keep synchronous repository pattern unless a feature strictly requires async.
- Keep panel width percent behavior aligned across MainWindow.axaml, MainWindow.axaml.cs, and App.axaml.cs.

Migration strategy:
- Continue additive SQLite migrations via EnsureColumnExists and table creation checks.
- Add schema version notes in code comments for each migration block.
- Always include safe defaults for new columns.

Performance strategy:
- Minimize full list reloads when in-place update is enough.
- Reuse existing filter reapply pattern.
- Add indexes for any new query dimensions (tag joins, archive state, reminder times).

## 7) Validation and QA Plan

Manual test matrix required for each milestone:
- Keyboard paths: add, edit, filter, select, execute actions, undo.
- Focus order and visible focus indicators.
- Width range checks at 80, 100, 150, 200 percent.
- High DPI checks (125 percent, 150 percent, 200 percent).
- Grouped and flat list behavior parity.
- Restart persistence checks for new settings.
- Legacy settings compatibility and migration safety.

Time-based feature checks:
- Timezone and DST boundary behavior.
- Reminder trigger idempotency after app hide/show and restart.
- Recurrence generation around month boundaries.

Regression checks:
- Inline rename behavior remains stable.
- Details panel expand/collapse suppression still prevents accidental closes.
- Tray show/hide, pin behavior, and auto-hide on deactivate still work.

## 8) Proposed Delivery Sequence (Detailed)

Phase 0 (1-2 days):
- Prepare small refactors to simplify filtering pipeline and command state management.
- Add internal helper methods for date-window logic and composable filtering.

Phase 1 (v1.1, 3-4 weeks):
- Build smart views and search first.
- Add undo framework and then integrate delete/reject/clear due date.
- Add multi-select and bulk actions with final keyboard/accessibility pass.

Phase 2 (v1.2, 4-5 weeks):
- Add reminders data model and scheduler.
- Add recurrence generation flow and harden with edge-case tests.
- Add tags and tag filters.

Phase 3 (v1.3, 4-6 weeks):
- Add export JSON/CSV and import path with preview.
- Add archive and retention policies.
- Add subtasks and parent progress UI.

## 9) Release Readiness Gates

A milestone is release-ready only when all are true:
- No data-loss bugs in destructive or migration paths.
- Keyboard-only completion of primary flows is possible.
- High DPI layout remains usable without clipped controls.
- Startup, tray interactions, and hide/show behaviors remain stable.
- Build passes with standard command:
  - dotnet build TodoList.slnx
  - If locked output issue appears, use the custom OutDir build command documented in AGENTS.md.

## 10) Open Decisions

Decisions needed before implementation starts:
- Should search query persist across restarts?
- Should undo support one step or a short stack in v1.1?
- Should recurring task completion auto-complete parent when subtasks are all done (once subtasks exist)?
- Should tag filtering use AND, OR, or user-selectable mode?
- Should retention archive or hard-delete old completed items?

Recommended defaults:
- Search persistence: off.
- Undo depth: one-step in v1.1.
- Tag filter semantics: AND (aligns with current filter model).
- Retention action: archive, never auto-delete.
