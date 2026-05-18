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
    private const int DefaultPageSize = 25;
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
    private readonly Dictionary<long, TodoItemViewModel> _todoById = new();
    private readonly DispatcherTimer _searchDebounceTimer;
    private int _currentPageStartIndex;
    private int _currentPageEndIndex;

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
        new SmartViewOption(TodoSmartView.DueSoon3Days, "Due soon (3 days)"),
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

    public IReadOnlyList<int> AvailablePageSizes { get; } =
    [
        10,
        25,
        50,
        100,
    ];

    [ObservableProperty]
    private bool isAddComposerOpen;

    [ObservableProperty]
    private bool isDeleteConfirmationOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmDeleteTodoCommand))]
    private long? deleteConfirmationTodoId;

    [ObservableProperty]
    private string deleteConfirmationTodoTitle = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveAddComposerTodoCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAndAddAnotherComposerTodoCommand))]
    private string addComposerTitle = string.Empty;

    [ObservableProperty]
    private TodoPriority addComposerPriority = TodoPriority.Normal;

    [ObservableProperty]
    private DateTimeOffset? addComposerDueAtLocal;

    [ObservableProperty]
    private string addComposerNotes = string.Empty;

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
    private string selectedGroupingDirection = OrderingDirectionDescendingOption;

    [ObservableProperty]
    private string selectedOrderingOption = OrderByCreationDateOption;

    [ObservableProperty]
    private string selectedOrderingDirection = OrderingDirectionDescendingOption;

    [ObservableProperty]
    private int pageSize = DefaultPageSize;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalFilteredCount;

    public bool GroupByDayAdded =>
        string.Equals(SelectedGroupingOption, GroupByDayAddedOption, StringComparison.Ordinal);

    public bool GroupByDueDate =>
        string.Equals(SelectedGroupingOption, GroupByDueDateOption, StringComparison.Ordinal);

    public bool GroupByPriority =>
        string.Equals(SelectedGroupingOption, GroupByPriorityOption, StringComparison.Ordinal);

    public bool ShowFlatList => !GroupByDayAdded && !GroupByDueDate && !GroupByPriority;

    public bool ShowGroupedList => !ShowFlatList;

    public bool IsGroupingDirectionEnabled => ShowGroupedList;

    public bool GroupDirectionAscending =>
        string.Equals(SelectedGroupingDirection, OrderingDirectionAscendingOption, StringComparison.Ordinal);

    public bool GroupDirectionDescending => !GroupDirectionAscending;

    public bool OrderByDueDate =>
        string.Equals(SelectedOrderingOption, OrderByDueDateOption, StringComparison.Ordinal);

    public bool OrderByCreationDate =>
        string.Equals(SelectedOrderingOption, OrderByCreationDateOption, StringComparison.Ordinal);

    public bool OrderByPriority =>
        string.Equals(SelectedOrderingOption, OrderByPriorityOption, StringComparison.Ordinal);

    public bool OrderDirectionAscending =>
        string.Equals(SelectedOrderingDirection, OrderingDirectionAscendingOption, StringComparison.Ordinal);

    public bool OrderDirectionDescending => !OrderDirectionAscending;

    public bool CanSaveAddComposerTodo => !string.IsNullOrWhiteSpace(AddComposerTitle);

    public bool CanConfirmDeleteTodo => DeleteConfirmationTodoId.HasValue;

    public string SummaryText =>
        $"{ActiveCount} active - {CompletedCount} completed - {RejectedCount} rejected";

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalFilteredCount / (double)Math.Max(1, PageSize)));

    public bool CanGoToPreviousPage => CurrentPage > 1;

    public bool CanGoToNextPage => CurrentPage < TotalPages;

    public string PaginationStatusText
    {
        get
        {
            if (TotalFilteredCount == 0)
            {
                return "No matching todos";
            }

            return $"Showing {_currentPageStartIndex}-{_currentPageEndIndex} of {TotalFilteredCount}";
        }
    }

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
            ResetCurrentPageForFilterChange();
            ApplyFilter();
        }
    }

    partial void OnSelectedSmartViewChanged(TodoSmartView value)
    {
        OnPropertyChanged(nameof(IsStatusFilterEnabled));
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnSelectedPriorityFilterChanged(TodoPriorityFilter value)
    {
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceTimer.Stop();
        ResetCurrentPageForFilterChange();
        _searchDebounceTimer.Start();
    }

    partial void OnIncludeNotesInSearchChanged(bool value)
    {
        _searchDebounceTimer.Stop();
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnPageSizeChanged(int value)
    {
        if (value <= 0)
        {
            PageSize = DefaultPageSize;
            return;
        }

        ResetCurrentPageForFilterChange();
        OnPaginationStateChanged();
        ApplyFilter();
    }

    partial void OnCurrentPageChanged(int value)
    {
        if (value <= 0)
        {
            CurrentPage = 1;
            return;
        }

        if (value > TotalPages)
        {
            CurrentPage = TotalPages;
            return;
        }

        OnPaginationStateChanged();
    }

    partial void OnTotalFilteredCountChanged(int value)
    {
        OnPaginationStateChanged();
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
        OnPropertyChanged(nameof(IsGroupingDirectionEnabled));
        ResetCurrentPageForFilterChange();
        ApplyFilter(rebuildInactiveBranch: true);
    }

    partial void OnSelectedGroupingDirectionChanged(string value)
    {
        OnPropertyChanged(nameof(GroupDirectionAscending));
        OnPropertyChanged(nameof(GroupDirectionDescending));
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnSelectedOrderingOptionChanged(string value)
    {
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnSelectedOrderingDirectionChanged(string value)
    {
        OnPropertyChanged(nameof(OrderDirectionAscending));
        OnPropertyChanged(nameof(OrderDirectionDescending));
        ResetCurrentPageForFilterChange();
        ApplyFilter();
    }

    partial void OnAddComposerTitleChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveAddComposerTodo));
    }

    partial void OnDeleteConfirmationTodoIdChanged(long? value)
    {
        OnPropertyChanged(nameof(CanConfirmDeleteTodo));
    }

    [RelayCommand]
    private void GoToPreviousPage()
    {
        if (!CanGoToPreviousPage)
        {
            return;
        }

        CurrentPage--;
        ApplyFilter();
    }

    [RelayCommand]
    private void GoToNextPage()
    {
        if (!CanGoToNextPage)
        {
            return;
        }

        CurrentPage++;
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenAddComposer()
    {
        if (IsAddComposerOpen)
        {
            return;
        }

        AddComposerTitle = string.Empty;
        AddComposerPriority = TodoPriority.Normal;
        AddComposerDueAtLocal = null;
        AddComposerNotes = string.Empty;
        IsAddComposerOpen = true;
    }

    [RelayCommand]
    private void CancelAddComposer()
    {
        if (!IsAddComposerOpen)
        {
            return;
        }

        IsAddComposerOpen = false;
        ResetAddComposerDraft();
    }

    [RelayCommand(CanExecute = nameof(CanSaveAddComposerTodo))]
    private void SaveAddComposerTodo()
    {
        if (!TryCreateTodo(AddComposerTitle, AddComposerPriority, AddComposerNotes, AddComposerDueAtLocal))
        {
            return;
        }

        IsAddComposerOpen = false;
        ResetAddComposerDraft();
    }

    [RelayCommand(CanExecute = nameof(CanSaveAddComposerTodo))]
    private void SaveAndAddAnotherComposerTodo()
    {
        if (!TryCreateTodo(AddComposerTitle, AddComposerPriority, AddComposerNotes, AddComposerDueAtLocal))
        {
            return;
        }

        ResetAddComposerDraft(preservePriority: true);
    }

    [RelayCommand]
    private void ClearAddComposerDueAtLocal()
    {
        if (!AddComposerDueAtLocal.HasValue)
        {
            return;
        }

        AddComposerDueAtLocal = null;
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
        SelectedGroupingDirection = OrderingDirectionDescendingOption;
        SelectedOrderingOption = OrderByCreationDateOption;
        SelectedOrderingDirection = OrderingDirectionDescendingOption;
    }

    [RelayCommand]
    private void ToggleGroupingDirection()
    {
        if (!IsGroupingDirectionEnabled)
        {
            return;
        }

        SelectedGroupingDirection = GroupDirectionAscending
            ? OrderingDirectionDescendingOption
            : OrderingDirectionAscendingOption;
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

        if (todo.IsRejected)
        {
            todo.IsCompleted = false;
            return;
        }

        _todoRepository.SetCompleted(todo.Id, todo.IsCompleted);
        RefreshSummaryCountsFromRepository();
        ApplyFilter();
    }

    [RelayCommand]
    private void RequestDeleteTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        DeleteConfirmationTodoId = todo.Id;
        DeleteConfirmationTodoTitle = todo.Title;
        IsDeleteConfirmationOpen = true;
    }

    [RelayCommand]
    private void CancelDeleteTodo()
    {
        if (!IsDeleteConfirmationOpen && !DeleteConfirmationTodoId.HasValue)
        {
            return;
        }

        ResetDeleteConfirmationState();
    }

    [RelayCommand(CanExecute = nameof(CanConfirmDeleteTodo))]
    private void ConfirmDeleteTodo()
    {
        if (!DeleteConfirmationTodoId.HasValue)
        {
            return;
        }

        var todoId = DeleteConfirmationTodoId.Value;
        _todoRepository.Delete(todoId);
        _todoById.Remove(todoId);
        RefreshSummaryCountsFromRepository();
        ApplyFilter();
        ResetDeleteConfirmationState();
    }

    [RelayCommand]
    private void RejectTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Reject(todo.Id);
        todo.IsRejected = true;
        todo.IsCompleted = false;
        RefreshSummaryCountsFromRepository();
        ApplyFilter();
    }

    [RelayCommand]
    private void RestoreTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Restore(todo.Id);
        todo.IsRejected = false;
        todo.IsCompleted = false;
        RefreshSummaryCountsFromRepository();
        ApplyFilter();
    }

    [RelayCommand]
    private void StartRenameTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        foreach (var item in EnumerateLoadedTodos())
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

        foreach (var item in EnumerateLoadedTodos())
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

        // Null due dates are handled by ClearTodoDueAtUtc to avoid accidental clears
        // from DatePicker rebind events during regroup/reorder operations.
        if (!todo.DueAtUtc.HasValue)
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
        foreach (var item in EnumerateLoadedTodos())
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
        var activeRenameTodo = EnumerateLoadedTodos().FirstOrDefault(todo => todo.IsRenaming);
        if (activeRenameTodo is null)
        {
            return;
        }

        CommitRenameTodo(activeRenameTodo);
    }

    private bool TryCreateTodo(
        string title,
        TodoPriority priority,
        string notes,
        DateTimeOffset? dueAtLocal)
    {
        var normalizedTitle = (title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        var todoId = _todoRepository.Add(normalizedTitle, priority);

        if (!string.IsNullOrEmpty(notes))
        {
            _todoRepository.UpdateNotes(todoId, notes);
        }

        if (dueAtLocal.HasValue)
        {
            _todoRepository.UpdateDueAtUtc(todoId, dueAtLocal.Value);
        }

        ResetCurrentPageForFilterChange();
        LoadTodos();
        return true;
    }

    private void ResetAddComposerDraft(bool preservePriority = false)
    {
        var previousPriority = AddComposerPriority;
        AddComposerTitle = string.Empty;
        AddComposerNotes = string.Empty;
        AddComposerDueAtLocal = null;
        AddComposerPriority = preservePriority ? previousPriority : TodoPriority.Normal;
    }

    private void ResetDeleteConfirmationState()
    {
        IsDeleteConfirmationOpen = false;
        DeleteConfirmationTodoId = null;
        DeleteConfirmationTodoTitle = string.Empty;
    }

    private void LoadTodos()
    {
        _todoById.Clear();
        RefreshSummaryCountsFromRepository();
        ApplyFilter();
    }

    private void RefreshSummaryCountsFromRepository()
    {
        var summaryCounts = _todoRepository.GetSummaryCounts();
        ActiveCount = summaryCounts.ActiveCount;
        CompletedCount = summaryCounts.CompletedCount;
        RejectedCount = summaryCounts.RejectedCount;
    }

    private void ApplyFilter(bool rebuildInactiveBranch = false)
    {
        var queryOptions = BuildTodoQueryOptions();
        TotalFilteredCount = _todoRepository.QueryCount(queryOptions);
        EnsureCurrentPageWithinRange();

        var safePageSize = Math.Max(1, PageSize);
        var pageOffset = (CurrentPage - 1) * safePageSize;
        if (ShowFlatList)
        {
            IReadOnlyList<TodoItem> pageTodos = TotalFilteredCount == 0
                ? Array.Empty<TodoItem>()
                : _todoRepository.QueryPage(queryOptions, safePageSize, pageOffset);

            var pagedFilteredList = BuildTodoViewModels(pageTodos);

            if (pagedFilteredList.Count == 0)
            {
                SetCurrentPageRange(0, 0);
            }
            else
            {
                SetCurrentPageRange(pageOffset + 1, pageOffset + pagedFilteredList.Count);
            }

            RefreshVisibleTodos(pagedFilteredList);
            if (rebuildInactiveBranch)
            {
                RebuildGroupedRows(pagedFilteredList);
            }

            OnPaginationStateChanged();
            return;
        }

        var groupedPagedTodos = BuildGroupedPageTodos(queryOptions, safePageSize, pageOffset);

        if (groupedPagedTodos.Count == 0)
        {
            SetCurrentPageRange(0, 0);
        }
        else
        {
            SetCurrentPageRange(pageOffset + 1, pageOffset + groupedPagedTodos.Count);
        }

        RebuildGroupedRows(groupedPagedTodos);
        if (rebuildInactiveBranch)
        {
            RefreshVisibleTodos(groupedPagedTodos);
        }

        OnPaginationStateChanged();
    }

    private List<TodoItemViewModel> BuildGroupedPageTodos(
        TodoQueryOptions queryOptions,
        int pageSize,
        int pageOffset)
    {
        if (TotalFilteredCount == 0)
        {
            return new List<TodoItemViewModel>();
        }

        var allFilteredTodos = _todoRepository.QueryPage(queryOptions, TotalFilteredCount, 0);
        var allFilteredTodoViewModels = BuildTodoViewModels(allFilteredTodos);
        var groupedOrderedTodos = BuildGroupedOrderedTodos(allFilteredTodoViewModels);
        return TakePage(groupedOrderedTodos, pageSize, pageOffset);
    }

    private List<TodoItemViewModel> BuildGroupedOrderedTodos(IReadOnlyList<TodoItemViewModel> todos)
    {
        var orderedTodos = new List<TodoItemViewModel>(todos.Count);

        if (GroupByDayAdded)
        {
            var groupedTodos = todos.GroupBy(todo => todo.CreatedAtUtc.ToLocalTime().Date);
            var orderedGroups = GroupDirectionAscending
                ? groupedTodos.OrderBy(group => group.Key)
                : groupedTodos.OrderByDescending(group => group.Key);

            foreach (var group in orderedGroups)
            {
                orderedTodos.AddRange(group);
            }

            return orderedTodos;
        }

        if (GroupByDueDate)
        {
            var groupedTodos = todos.GroupBy(todo => todo.DueAtUtc?.Date);

            var orderedGroups = GroupDirectionAscending
                ? groupedTodos
                    .OrderBy(group => group.Key.HasValue ? 0 : 1)
                    .ThenBy(group => group.Key ?? DateTime.MaxValue)
                : groupedTodos
                    .OrderBy(group => group.Key.HasValue ? 0 : 1)
                    .ThenByDescending(group => group.Key ?? DateTime.MinValue);

            foreach (var group in orderedGroups)
            {
                orderedTodos.AddRange(group);
            }

            return orderedTodos;
        }

        if (GroupByPriority)
        {
            var groupedTodos = todos.GroupBy(todo => todo.Priority);
            var orderedGroups = GroupDirectionAscending
                ? groupedTodos.OrderBy(group => group.Key)
                : groupedTodos.OrderByDescending(group => group.Key);

            foreach (var group in orderedGroups)
            {
                orderedTodos.AddRange(group);
            }

            return orderedTodos;
        }

        orderedTodos.AddRange(todos);
        return orderedTodos;
    }

    private static List<TodoItemViewModel> TakePage(
        IReadOnlyList<TodoItemViewModel> todos,
        int pageSize,
        int pageOffset)
    {
        if (pageOffset >= todos.Count)
        {
            return new List<TodoItemViewModel>();
        }

        var endExclusive = Math.Min(pageOffset + pageSize, todos.Count);
        var page = new List<TodoItemViewModel>(endExclusive - pageOffset);

        for (var index = pageOffset; index < endExclusive; index++)
        {
            page.Add(todos[index]);
        }

        return page;
    }

    private List<TodoItemViewModel> BuildTodoViewModels(IReadOnlyList<TodoItem> todos)
    {
        var pageViewModels = new List<TodoItemViewModel>(todos.Count);

        foreach (var todo in todos)
        {
            if (_todoById.TryGetValue(todo.Id, out var existingViewModel))
            {
                UpdateTodoViewModel(existingViewModel, todo);
                pageViewModels.Add(existingViewModel);
                continue;
            }

            var viewModel = TodoItemViewModel.From(todo);
            _todoById[viewModel.Id] = viewModel;
            pageViewModels.Add(viewModel);
        }

        return pageViewModels;
    }

    private static void UpdateTodoViewModel(TodoItemViewModel viewModel, TodoItem todo)
    {
        if (!viewModel.IsRenaming)
        {
            viewModel.Title = todo.Title;
        }

        viewModel.Notes = todo.Notes;
        viewModel.Priority = todo.Priority;
        viewModel.DueAtUtc = todo.DueAtUtc?.ToLocalTime();
        viewModel.IsCompleted = todo.IsCompleted;
        viewModel.IsRejected = todo.IsRejected;
    }

    private IEnumerable<TodoItemViewModel> EnumerateLoadedTodos()
    {
        return _todoById.Values;
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
            DueSoonThreeDaysEndExclusiveUtcUnix = LocalDateStartToUtcUnix(today.AddDays(4)),
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

    private void ResetCurrentPageForFilterChange()
    {
        if (CurrentPage != 1)
        {
            CurrentPage = 1;
        }
    }

    private void EnsureCurrentPageWithinRange()
    {
        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }
    }

    private void SetCurrentPageRange(int startIndex, int endIndex)
    {
        _currentPageStartIndex = startIndex;
        _currentPageEndIndex = endIndex;
    }

    private void OnPaginationStateChanged()
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
        OnPropertyChanged(nameof(PaginationStatusText));
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
            .GroupBy(todo => todo.CreatedAtUtc.ToLocalTime().Date);

        var orderedGroups = GroupDirectionAscending
            ? groupedTodos.OrderBy(group => group.Key)
            : groupedTodos.OrderByDescending(group => group.Key);

        foreach (var dayGroup in orderedGroups)
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
            .GroupBy(todo => todo.Priority);

        var orderedGroups = GroupDirectionAscending
            ? groupedTodos.OrderBy(group => group.Key)
            : groupedTodos.OrderByDescending(group => group.Key);

        foreach (var priorityGroup in orderedGroups)
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

        var orderedGroups = GroupDirectionAscending
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
