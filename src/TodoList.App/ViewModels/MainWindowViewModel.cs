using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private const string GroupByPriorityOption = "Priority";

    private readonly ITodoRepository _todoRepository;
    private readonly List<TodoItemViewModel> _allTodos = new();

    public ObservableCollection<TodoItemViewModel> VisibleTodos { get; } = new();

    public ObservableCollection<TodoDayGroupViewModel> VisibleTodoGroups { get; } = new();

    public IReadOnlyList<TodoFilter> AvailableFilters { get; } =
    [
        TodoFilter.Active,
        TodoFilter.Completed,
        TodoFilter.Rejected,
        TodoFilter.All,
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
        GroupByPriorityOption,
    ];

    [ObservableProperty]
    private string newTodoText = string.Empty;

    [ObservableProperty]
    private TodoPriority newTodoPriority = TodoPriority.Normal;

    [ObservableProperty]
    private TodoFilter selectedFilter = TodoFilter.Active;

    [ObservableProperty]
    private TodoPriorityFilter selectedPriorityFilter = TodoPriorityFilter.All;

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

    public bool GroupByDayAdded =>
        string.Equals(SelectedGroupingOption, GroupByDayAddedOption, StringComparison.Ordinal);

    public bool GroupByPriority =>
        string.Equals(SelectedGroupingOption, GroupByPriorityOption, StringComparison.Ordinal);

    public bool ShowFlatList => !GroupByDayAdded && !GroupByPriority;

    public bool ShowGroupedList => !ShowFlatList;

    public string SummaryText =>
        $"{ActiveCount} active - {CompletedCount} completed - {RejectedCount} rejected";

    public MainWindowViewModel(ITodoRepository todoRepository)
    {
        _todoRepository = todoRepository;
        LoadTodos();
    }

    partial void OnSelectedFilterChanged(TodoFilter value)
    {
        ApplyFilter();
    }

    partial void OnSelectedPriorityFilterChanged(TodoPriorityFilter value)
    {
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
        OnPropertyChanged(nameof(GroupByPriority));
        OnPropertyChanged(nameof(ShowFlatList));
        OnPropertyChanged(nameof(ShowGroupedList));
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
    private void ToggleCompleted(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        if (todo.IsRejected)
        {
            LoadTodos();
            return;
        }

        _todoRepository.SetCompleted(todo.Id, todo.IsCompleted);
        LoadTodos();
    }

    [RelayCommand]
    private void DeleteTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Delete(todo.Id);
        LoadTodos();
    }

    [RelayCommand]
    private void RejectTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Reject(todo.Id);
        LoadTodos();
    }

    [RelayCommand]
    private void RestoreTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
        {
            return;
        }

        _todoRepository.Restore(todo.Id);
        LoadTodos();
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

        foreach (var todo in _todoRepository.GetAll())
        {
            _allTodos.Add(TodoItemViewModel.From(todo));
        }

        ActiveCount = _allTodos.Count(todo => !todo.IsCompleted && !todo.IsRejected);
        CompletedCount = _allTodos.Count(todo => todo.IsCompleted && !todo.IsRejected);
        RejectedCount = _allTodos.Count(todo => todo.IsRejected);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<TodoItemViewModel> filteredTodos = SelectedFilter switch
        {
            TodoFilter.Active => _allTodos.Where(todo => !todo.IsCompleted && !todo.IsRejected),
            TodoFilter.Completed => _allTodos.Where(todo => todo.IsCompleted && !todo.IsRejected),
            TodoFilter.Rejected => _allTodos.Where(todo => todo.IsRejected),
            _ => _allTodos,
        };

        filteredTodos = SelectedPriorityFilter switch
        {
            TodoPriorityFilter.Minor => filteredTodos.Where(todo => todo.Priority == TodoPriority.Minor),
            TodoPriorityFilter.Normal => filteredTodos.Where(todo => todo.Priority == TodoPriority.Normal),
            TodoPriorityFilter.Major => filteredTodos.Where(todo => todo.Priority == TodoPriority.Major),
            TodoPriorityFilter.Critical => filteredTodos.Where(todo => todo.Priority == TodoPriority.Critical),
            _ => filteredTodos,
        };

        var filteredList = filteredTodos.ToList();

        VisibleTodos.Clear();
        foreach (var todo in filteredList)
        {
            VisibleTodos.Add(todo);
        }

        RebuildGroups(filteredList);
    }

    private void RebuildGroups(IReadOnlyList<TodoItemViewModel> todos)
    {
        if (GroupByDayAdded)
        {
            RebuildDayGroups(todos);
            return;
        }

        if (GroupByPriority)
        {
            RebuildPriorityGroups(todos);
            return;
        }

        VisibleTodoGroups.Clear();
    }

    private void RebuildDayGroups(IReadOnlyList<TodoItemViewModel> todos)
    {
        VisibleTodoGroups.Clear();

        var groupedTodos = todos
            .GroupBy(todo => todo.CreatedAtUtc.ToLocalTime().Date)
            .OrderByDescending(group => group.Key);

        foreach (var dayGroup in groupedTodos)
        {
            var header = BuildDayHeader(dayGroup.Key);
            VisibleTodoGroups.Add(
                new TodoDayGroupViewModel(
                    header,
                    dayGroup.OrderByDescending(todo => todo.CreatedAtUtc)));
        }
    }

    private void RebuildPriorityGroups(IReadOnlyList<TodoItemViewModel> todos)
    {
        VisibleTodoGroups.Clear();

        var groupedTodos = todos
            .GroupBy(todo => todo.Priority)
            .OrderByDescending(group => group.Key);

        foreach (var priorityGroup in groupedTodos)
        {
            VisibleTodoGroups.Add(
                new TodoDayGroupViewModel(
                    BuildPriorityHeader(priorityGroup.Key),
                    priorityGroup.OrderByDescending(todo => todo.CreatedAtUtc)));
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
}

public sealed class TodoDayGroupViewModel
{
    public string Header { get; }

    public ObservableCollection<TodoItemViewModel> Items { get; }

    public TodoDayGroupViewModel(string header, IEnumerable<TodoItemViewModel> items)
    {
        Header = header;
        Items = new ObservableCollection<TodoItemViewModel>(items);
    }
}
