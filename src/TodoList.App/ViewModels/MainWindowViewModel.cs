using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TodoList.App.Data;
using TodoList.App.Models;

namespace TodoList.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string NoGroupingOption = "None";
    private const string GroupByDayAddedOption = "Added day";
    private const string GroupByDueDateOption = "Due date";
    private const string GroupByPriorityOption = "Priority";
    private const string NoDueDateGroupHeader = "No due date";
    private const string OrderByDueDateOption = "Due date";
    private const string OrderByCreationDateOption = "Creation date";
    private const string OrderByPriorityOption = "Priority";
    private const string OrderingDirectionDescendingOption = "Descending";
    private const string OrderingDirectionAscendingOption = "Ascending";
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(200);

    private readonly ITodoRepository _todoRepository;
    private readonly List<TodoItemViewModel> _allTodos = new();
    private readonly Dictionary<long, TodoItemViewModel> _todoById = new();
    private readonly DispatcherTimer _searchDebounceTimer;

    public ObservableCollection<TodoItemViewModel> VisibleTodos { get; } = new();

    public ObservableCollection<TodoGroupedRowViewModel> VisibleGroupedRows { get; } = new();

    public IReadOnlyList<TodoFilter> AvailableFilters { get; } =
    [
        TodoFilter.All,
        TodoFilter.Active,
        TodoFilter.Completed,
        TodoFilter.Rejected,
    ];

    public IReadOnlyList<SmartViewOption> AvailableSmartViews { get; } =
    [
        new SmartViewOption(TodoSmartView.None, "None"),
        new SmartViewOption(TodoSmartView.Today, "Due today"),
        new SmartViewOption(TodoSmartView.DueSoon, "Due soon (7 days)"),
        new SmartViewOption(TodoSmartView.Overdue, "Overdue"),
    ];

    public IReadOnlyList<TodoPriorityFilter> AvailablePriorityFilters { get; } =
    [
        TodoPriorityFilter.All,
        TodoPriorityFilter.Minor,
        TodoPriorityFilter.Normal,
        TodoPriorityFilter.Major,
        TodoPriorityFilter.Critical,
    ];

    public IReadOnlyList<TodoPriority> AvailablePriorities { get; } =
    [
        TodoPriority.Minor,
        TodoPriority.Normal,
        TodoPriority.Major,
        TodoPriority.Critical,
    ];

    public IReadOnlyList<string> AvailableGroupingOptions { get; } =
    [
        NoGroupingOption,
        GroupByDayAddedOption,
        GroupByDueDateOption,
        GroupByPriorityOption,
    ];

    public IReadOnlyList<string> AvailableOrderingOptions { get; } =
    [
        OrderByCreationDateOption,
        OrderByDueDateOption,
        OrderByPriorityOption,
    ];

    public IReadOnlyList<string> AvailableOrderingDirections { get; } =
    [
        OrderingDirectionDescendingOption,
        OrderingDirectionAscendingOption,
    ];

    [ObservableProperty]
    private string newTodoText = string.Empty;

    [ObservableProperty]
    private TodoPriority newTodoPriority = TodoPriority.Normal;

    [ObservableProperty]
    private TodoFilter selectedFilter = TodoFilter.Active;

    [ObservableProperty]
    private TodoSmartView selectedSmartView = TodoSmartView.None;

    [ObservableProperty]
    private TodoPriorityFilter selectedPriorityFilter = TodoPriorityFilter.All;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private bool includeNotesInSearch;

    [ObservableProperty]
    private int activeCount;

    [ObservableProperty]
    private int completedCount;

    [ObservableProperty]
    private int rejectedCount;

    [ObservableProperty]
    private bool isPinned;

    [ObservableProperty]
    private string selectedGroupingOption = NoGroupingOption;

    [ObservableProperty]
    private string selectedOrderingOption = OrderByCreationDateOption;

    [ObservableProperty]
    private string selectedOrderingDirection = OrderingDirectionDescendingOption;

    public bool GroupByDayAdded =>
        string.Equals(SelectedGroupingOption, GroupByDayAddedOption, StringComparison.Ordinal);

    public bool GroupByDueDate =>
        string.Equals(SelectedGroupingOption, GroupByDueDateOption, StringComparison.Ordinal);

    public bool GroupByPriority =>
        string.Equals(SelectedGroupingOption, GroupByPriorityOption, StringComparison.Ordinal);

    public bool ShowFlatList => !GroupByDayAdded && !GroupByDueDate && !GroupByPriority;

    public bool ShowGroupedList => !ShowFlatList;

    public bool OrderByDueDate =>
        string.Equals(SelectedOrderingOption, OrderByDueDateOption, StringComparison.Ordinal);

    public bool OrderByCreationDate =>
        string.Equals(SelectedOrderingOption, OrderByCreationDateOption, StringComparison.Ordinal);

    public bool OrderByPriority =>
        string.Equals(SelectedOrderingOption, OrderByPriorityOption, StringComparison.Ordinal);

    public bool OrderDirectionAscending =>
        string.Equals(SelectedOrderingDirection, OrderingDirectionAscendingOption, StringComparison.Ordinal);

    public bool OrderDirectionDescending => !OrderDirectionAscending;

    public string SummaryText =>
        $"{ActiveCount} active - {CompletedCount} completed - {RejectedCount} rejected";

    public bool IsStatusFilterEnabled => SelectedSmartView == TodoSmartView.None;

    public MainWindowViewModel(ITodoRepository todoRepository)
    {
        _todoRepository = todoRepository;

        _searchDebounceTimer = new DispatcherTimer
        {
            Interval = SearchDebounceDelay,
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            ApplyFilter();
        };

        LoadTodos();
    }

    partial void OnSelectedFilterChanged(TodoFilter value)
    {
        if (SelectedSmartView == TodoSmartView.None)
        {
            ApplyFilter();
        }
    }

    partial void OnSelectedSmartViewChanged(TodoSmartView value)
    {
        OnPropertyChanged(nameof(IsStatusFilterEnabled));
        ApplyFilter();
    }

    partial void OnSelectedPriorityFilterChanged(TodoPriorityFilter value)
    {
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    partial void OnIncludeNotesInSearchChanged(bool value)
    {
        _searchDebounceTimer.Stop();
        ApplyFilter();
    }

    partial void OnActiveCountChanged(int value)
    {
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnCompletedCountChanged(int value)
    {
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnRejectedCountChanged(int value)
    {
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnSelectedGroupingOptionChanged(string value)
    {
        OnPropertyChanged(nameof(GroupByDayAdded));
        OnPropertyChanged(nameof(GroupByDueDate));
        OnPropertyChanged(nameof(GroupByPriority));
        OnPropertyChanged(nameof(ShowFlatList));
        OnPropertyChanged(nameof(ShowGroupedList));
        ApplyFilter(rebuildInactiveBranch: true);
    }

    partial void OnSelectedOrderingOptionChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedOrderingDirectionChanged(string value)
    {
        OnPropertyChanged(nameof(OrderDirectionAscending));
        OnPropertyChanged(nameof(OrderDirectionDescending));
        ApplyFilter();
    }

    [RelayCommand]
    private void AddTodo()
    {
        var title = NewTodoText.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _todoRepository.Add(title, NewTodoPriority);
        NewTodoText = string.Empty;
        LoadTodos();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        if (!string.IsNullOrEmpty(SearchQuery))
        {
            SearchQuery = string.Empty;
            _searchDebounceTimer.Stop();
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void ClearGeneralFilters()
    {
        SelectedSmartView = TodoSmartView.None;
        SelectedFilter = TodoFilter.Active;
        SelectedPriorityFilter = TodoPriorityFilter.All;
        SelectedGroupingOption = NoGroupingOption;
        SelectedOrderingOption = OrderByCreationDateOption;
        SelectedOrderingDirection = OrderingDirectionDescendingOption;
    }

    [RelayCommand]
    private void ToggleOrderingDirection()
    {
        SelectedOrderingDirection = OrderDirectionAscending
            ? OrderingDirectionDescendingOption
            : OrderingDirectionAscendingOption;
    }

    [RelayCommand]
    private void ToggleCompleted(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        if (!_todoById.ContainsKey(todo.Id))
        {
            LoadTodos();
            return;
        }

        if (todo.IsRejected)
        {
            todo.IsCompleted = false;
            return;
        }

        _todoRepository.SetCompleted(todo.Id, todo.IsCompleted);
        RecalculateSummaryCounts();
        ApplyFilter();
    }

    [RelayCommand]
    private void DeleteTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Delete(todo.Id);

        if (RemoveTodoFromAllTodos(todo.Id))
        {
            RecalculateSummaryCounts();
            ApplyFilter();
            return;
        }

        LoadTodos();
    }

    [RelayCommand]
    private void RejectTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        if (!_todoById.ContainsKey(todo.Id))
        {
            LoadTodos();
            return;
        }

        _todoRepository.Reject(todo.Id);
        todo.IsRejected = true;
        todo.IsCompleted = false;
        RecalculateSummaryCounts();
        ApplyFilter();
    }

    [RelayCommand]
    private void RestoreTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        if (!_todoById.ContainsKey(todo.Id))
        {
            LoadTodos();
            return;
        }

        _todoRepository.Restore(todo.Id);
        todo.IsRejected = false;
        todo.IsCompleted = false;
        RecalculateSummaryCounts();
        ApplyFilter();
    }

    [RelayCommand]
    private void StartRenameTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        foreach (var item in _allTodos)
        {
            if (!ReferenceEquals(item, todo) && item.IsRenaming)
            {
                item.CancelRename();
            }
        }

        todo.BeginRename();
    }

    [RelayCommand]
    private void ToggleTodoDetails(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        var shouldExpand = !todo.IsDetailsExpanded;

        foreach (var item in _allTodos)
        {
            if (!ReferenceEquals(item, todo) && item.IsDetailsExpanded)
            {
                item.IsDetailsExpanded = false;
            }
        }

        todo.IsDetailsExpanded = shouldExpand;
    }

    [RelayCommand]
    private void SaveTodoNotes(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.UpdateNotes(todo.Id, todo.Notes ?? string.Empty);
    }

    [RelayCommand]
    private void SaveTodoPriority(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.UpdatePriority(todo.Id, todo.Priority);
        ApplyFilter();
    }

    [RelayCommand]
    private void SaveTodoDueAtUtc(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        var normalizedDueAtUtc = todo.DueAtUtc?.ToUniversalTime();

        _todoRepository.UpdateDueAtUtc(todo.Id, normalizedDueAtUtc);

        if (OrderByDueDate || GroupByDueDate)
        {
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void ClearTodoDueAtUtc(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        if (!todo.DueAtUtc.HasValue)
        {
            return;
        }

        todo.DueAtUtc = null;
        _todoRepository.UpdateDueAtUtc(todo.Id, null);

        if (OrderByDueDate || GroupByDueDate)
        {
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void CollapseTodoDetails()
    {
        foreach (var item in _allTodos)
        {
            if (item.IsDetailsExpanded)
            {
                item.IsDetailsExpanded = false;
            }
        }
    }

    [RelayCommand]
    private void CommitRenameTodo(TodoItemViewModel? todo)
    {
        if (todo is null || !todo.IsRenaming)
        {
            return;
        }

        var renamedTitle = (todo.RenameText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(renamedTitle))
        {
            todo.CancelRename();
            return;
        }

        if (string.Equals(renamedTitle, todo.Title, StringComparison.Ordinal))
        {
            todo.CancelRename();
            return;
        }

        _todoRepository.Rename(todo.Id, renamedTitle);
        todo.Title = renamedTitle;
        todo.CancelRename();
        ApplyFilter();
    }

    [RelayCommand]
    private void CancelRenameTodo(TodoItemViewModel? todo)
    {
        todo?.CancelRename();
    }

    public void CommitActiveRename()
    {
        var activeRenameTodo = _allTodos.FirstOrDefault(todo => todo.IsRenaming);
        if (activeRenameTodo is null)
        {
            return;
        }

        CommitRenameTodo(activeRenameTodo);
    }

    private void LoadTodos()
    {
        _allTodos.Clear();
        _todoById.Clear();

        foreach (var todo in _todoRepository.GetAll())
        {
            var todoViewModel = TodoItemViewModel.From(todo);
            _allTodos.Add(todoViewModel);
            _todoById[todoViewModel.Id] = todoViewModel;
        }

        RecalculateSummaryCounts();
        ApplyFilter();
    }

    private void RecalculateSummaryCounts()
    {
        ActiveCount = _allTodos.Count(todo => !todo.IsCompleted && !todo.IsRejected);
        CompletedCount = _allTodos.Count(todo => todo.IsCompleted && !todo.IsRejected);
        RejectedCount = _allTodos.Count(todo => todo.IsRejected);
    }

    private bool RemoveTodoFromAllTodos(long todoId)
    {
        var index = _allTodos.FindIndex(todo => todo.Id == todoId);
        if (index < 0)
        {
            return false;
        }

        var todoToRemove = _allTodos[index];
        _allTodos.RemoveAt(index);
        _todoById.Remove(todoToRemove.Id);
        return true;
    }

    private void ApplyFilter(bool rebuildInactiveBranch = false)
    {
        if (!TryGetFilteredTodosFromRepository(out var filteredList))
        {
            return;
        }

        if (ShowFlatList)
        {
            RefreshVisibleTodos(filteredList);
            if (rebuildInactiveBranch)
            {
                RebuildGroupedRows(filteredList);
            }

            return;
        }

        RebuildGroupedRows(filteredList);
        if (rebuildInactiveBranch)
        {
            RefreshVisibleTodos(filteredList);
        }
    }

    private bool TryGetFilteredTodosFromRepository(out List<TodoItemViewModel> filteredTodos)
    {
        var queryOptions = BuildTodoQueryOptions();
        var filteredTodoIds = _todoRepository.QueryIds(queryOptions);

        filteredTodos = new List<TodoItemViewModel>(filteredTodoIds.Count);

        foreach (var todoId in filteredTodoIds)
        {
            if (_todoById.TryGetValue(todoId, out var todo))
            {
                filteredTodos.Add(todo);
                continue;
            }

            LoadTodos();
            filteredTodos = new List<TodoItemViewModel>();
            return false;
        }

        return true;
    }

    private TodoQueryOptions BuildTodoQueryOptions()
    {
        var today = DateTime.Now.Date;

        return new TodoQueryOptions
        {
            SmartView = SelectedSmartView,
            StatusFilter = SelectedFilter,
            PriorityFilter = SelectedPriorityFilter,
            SearchQuery = SearchQuery,
            IncludeNotesInSearch = IncludeNotesInSearch,
            Ordering = ResolveSelectedOrdering(),
            OrderAscending = OrderDirectionAscending,
            TodayStartUtcUnix = LocalDateStartToUtcUnix(today),
            TomorrowStartUtcUnix = LocalDateStartToUtcUnix(today.AddDays(1)),
            DueSoonEndExclusiveUtcUnix = LocalDateStartToUtcUnix(today.AddDays(8)),
        };
    }

    private TodoOrdering ResolveSelectedOrdering()
    {
        if (OrderByDueDate)
        {
            return TodoOrdering.DueDate;
        }

        if (OrderByPriority)
        {
            return TodoOrdering.Priority;
        }

        return TodoOrdering.CreationDate;
    }

    private static long LocalDateStartToUtcUnix(DateTime localDate)
    {
        var localStart = DateTime.SpecifyKind(localDate.Date, DateTimeKind.Local);
        return new DateTimeOffset(localStart).ToUniversalTime().ToUnixTimeSeconds();
    }

    private void RebuildGroupedRows(IReadOnlyList<TodoItemViewModel> todos)
    {
        var existingRowsByKey = BuildExistingGroupedRowLookup();
        var nextRows = new List<TodoGroupedRowViewModel>(Math.Max(VisibleGroupedRows.Count, todos.Count + 8));

        if (GroupByDayAdded)
        {
            RebuildDayGroupedRows(todos, nextRows, existingRowsByKey);
        }
        else if (GroupByDueDate)
        {
            RebuildDueDateGroupedRows(todos, nextRows, existingRowsByKey);
        }
        else if (GroupByPriority)
        {
            RebuildPriorityGroupedRows(todos, nextRows, existingRowsByKey);
        }

        SyncCollectionByReference(VisibleGroupedRows, nextRows);
    }

    private void RefreshVisibleTodos(IReadOnlyList<TodoItemViewModel> todos)
    {
        SyncCollectionByReference(VisibleTodos, todos);
    }

    private void RebuildDayGroupedRows(
        IReadOnlyList<TodoItemViewModel> todos,
        ICollection<TodoGroupedRowViewModel> destination,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        var groupedTodos = todos
            .GroupBy(todo => todo.CreatedAtUtc.ToLocalTime().Date)
            .OrderByDescending(group => group.Key);

        foreach (var dayGroup in groupedTodos)
        {
            AddGroupedRows(
                BuildDayHeader(dayGroup.Key),
                dayGroup,
                destination,
                existingRowsByKey);
        }
    }

    private void RebuildPriorityGroupedRows(
        IReadOnlyList<TodoItemViewModel> todos,
        ICollection<TodoGroupedRowViewModel> destination,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        var groupedTodos = todos
            .GroupBy(todo => todo.Priority)
            .OrderByDescending(group => group.Key);

        foreach (var priorityGroup in groupedTodos)
        {
            AddGroupedRows(
                BuildPriorityHeader(priorityGroup.Key),
                priorityGroup,
                destination,
                existingRowsByKey);
        }
    }

    private void RebuildDueDateGroupedRows(
        IReadOnlyList<TodoItemViewModel> todos,
        ICollection<TodoGroupedRowViewModel> destination,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        var groupedTodos = todos.GroupBy(todo => todo.DueAtUtc?.Date);

        var orderedGroups = OrderDirectionAscending
            ? groupedTodos
                .OrderBy(group => group.Key.HasValue ? 0 : 1)
                .ThenBy(group => group.Key ?? DateTime.MaxValue)
            : groupedTodos
                .OrderBy(group => group.Key.HasValue ? 0 : 1)
                .ThenByDescending(group => group.Key ?? DateTime.MinValue);

        foreach (var dueDateGroup in orderedGroups)
        {
            var header = dueDateGroup.Key.HasValue
                ? BuildDueDateHeader(dueDateGroup.Key.Value)
                : NoDueDateGroupHeader;

            AddGroupedRows(
                header,
                dueDateGroup,
                destination,
                existingRowsByKey);
        }
    }

    private void AddGroupedRows(
        string header,
        IEnumerable<TodoItemViewModel> todos,
        ICollection<TodoGroupedRowViewModel> destination,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        destination.Add(GetOrCreateHeaderRow(header, existingRowsByKey));

        foreach (var todo in todos)
        {
            destination.Add(GetOrCreateTodoRow(todo, existingRowsByKey));
        }
    }

    private Dictionary<GroupedRowKey, TodoGroupedRowViewModel> BuildExistingGroupedRowLookup()
    {
        var lookup = new Dictionary<GroupedRowKey, TodoGroupedRowViewModel>();

        foreach (var row in VisibleGroupedRows)
        {
            var key = GetGroupedRowKey(row);
            if (!lookup.ContainsKey(key))
            {
                lookup.Add(key, row);
            }
        }

        return lookup;
    }

    private static GroupedRowKey GetGroupedRowKey(TodoGroupedRowViewModel row)
    {
        if (row.IsHeader)
        {
            return GroupedRowKey.ForHeader(row.Header);
        }

        return row.Todo is null
            ? GroupedRowKey.ForTodo(0)
            : GroupedRowKey.ForTodo(row.Todo.Id);
    }

    private static TodoGroupedRowViewModel GetOrCreateHeaderRow(
        string header,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        var key = GroupedRowKey.ForHeader(header);
        if (existingRowsByKey.TryGetValue(key, out var row))
        {
            return row;
        }

        return TodoGroupedRowViewModel.CreateHeader(header);
    }

    private static TodoGroupedRowViewModel GetOrCreateTodoRow(
        TodoItemViewModel todo,
        IReadOnlyDictionary<GroupedRowKey, TodoGroupedRowViewModel> existingRowsByKey)
    {
        var key = GroupedRowKey.ForTodo(todo.Id);
        if (existingRowsByKey.TryGetValue(key, out var row)
            && row.IsTodo
            && ReferenceEquals(row.Todo, todo))
        {
            return row;
        }

        return TodoGroupedRowViewModel.CreateTodo(todo);
    }

    private static void SyncCollectionByReference<TItem>(
        ObservableCollection<TItem> target,
        IReadOnlyList<TItem> desired)
        where TItem : class
    {
        if (target.Count == desired.Count)
        {
            var sameOrder = true;
            for (var index = 0; index < desired.Count; index++)
            {
                if (!ReferenceEquals(target[index], desired[index]))
                {
                    sameOrder = false;
                    break;
                }
            }

            if (sameOrder)
            {
                return;
            }
        }

        var desiredItems = new HashSet<TItem>(desired);

        for (var index = target.Count - 1; index >= 0; index--)
        {
            if (!desiredItems.Contains(target[index]))
            {
                target.RemoveAt(index);
            }
        }

        for (var index = 0; index < desired.Count; index++)
        {
            var desiredItem = desired[index];

            if (index < target.Count && ReferenceEquals(target[index], desiredItem))
            {
                continue;
            }

            var existingIndex = IndexOfReference(target, desiredItem, index + 1);
            if (existingIndex >= 0)
            {
                target.Move(existingIndex, index);
                continue;
            }

            target.Insert(index, desiredItem);
        }

        while (target.Count > desired.Count)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    private static int IndexOfReference<TItem>(
        IList<TItem> items,
        TItem value,
        int startIndex)
        where TItem : class
    {
        for (var index = Math.Max(0, startIndex); index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], value))
            {
                return index;
            }
        }

        return -1;
    }

    private readonly record struct GroupedRowKey(bool IsHeader, string Header, long TodoId)
    {
        public static GroupedRowKey ForHeader(string header)
        {
            return new GroupedRowKey(true, header, 0);
        }

        public static GroupedRowKey ForTodo(long todoId)
        {
            return new GroupedRowKey(false, string.Empty, todoId);
        }
    }

    private static string BuildDayHeader(DateTime localDate)
    {
        var today = DateTime.Now.Date;
        if (localDate == today)
        {
            return "Today";
        }

        if (localDate == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return localDate.ToString("dddd, dd MMM yyyy");
    }

    private static string BuildPriorityHeader(TodoPriority priority)
    {
        return priority.ToString();
    }

    private static string BuildDueDateHeader(DateTime localDate)
    {
        var today = DateTime.Now.Date;
        if (localDate == today)
        {
            return "Today";
        }

        if (localDate == today.AddDays(1))
        {
            return "Tomorrow";
        }

        if (localDate == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return localDate.ToString("dddd, dd MMM yyyy");
    }
}

public sealed class TodoGroupedRowViewModel
{
    private TodoGroupedRowViewModel(string header)
    {
        Header = header;
        IsHeader = true;
    }

    private TodoGroupedRowViewModel(TodoItemViewModel todo)
    {
        Todo = todo;
    }

    public bool IsHeader { get; }

    public bool IsTodo => !IsHeader;

    public string Header { get; } = string.Empty;

    public TodoItemViewModel? Todo { get; }

    public static TodoGroupedRowViewModel CreateHeader(string header)
    {
        return new TodoGroupedRowViewModel(header);
    }

    public static TodoGroupedRowViewModel CreateTodo(TodoItemViewModel todo)
    {
        return new TodoGroupedRowViewModel(todo);
    }
}

public sealed class SmartViewOption
{
    public TodoSmartView Value { get; }

    public string Label { get; }

    public SmartViewOption(TodoSmartView value, string label)
    {
        Value = value;
        Label = label;
    }
}
