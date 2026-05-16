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

    public IReadOnlyList<string> AvailableGroupingOptions { get; } =
    [
        NoGroupingOption,
        GroupByDayAddedOption,
    ];

    [ObservableProperty]
    private string newTodoText = string.Empty;

    [ObservableProperty]
    private TodoFilter selectedFilter = TodoFilter.Active;

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

    public bool ShowFlatList => !GroupByDayAdded;

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
        OnPropertyChanged(nameof(ShowFlatList));
    }

    [RelayCommand]
    private void AddTodo()
    {
        var title = NewTodoText.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        _todoRepository.Add(title);
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

        var filteredList = filteredTodos.ToList();

        VisibleTodos.Clear();
        foreach (var todo in filteredList)
        {
            VisibleTodos.Add(todo);
        }

        RebuildDayGroups(filteredList);
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
