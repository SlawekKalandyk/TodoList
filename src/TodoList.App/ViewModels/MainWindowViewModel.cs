using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TodoList.App.Data;
using TodoList.App.Models;

namespace TodoList.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITodoRepository _todoRepository;
    private readonly List<TodoItemViewModel> _allTodos = new();

    public ObservableCollection<TodoItemViewModel> VisibleTodos { get; } = new();

    public IReadOnlyList<TodoFilter> AvailableFilters { get; } =
    [
        TodoFilter.Active,
        TodoFilter.Completed,
        TodoFilter.All,
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
    private bool isPinned;

    public string SummaryText => $"{ActiveCount} active - {CompletedCount} completed";

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
    private void ClearCompleted()
    {
        _todoRepository.DeleteCompleted();
        LoadTodos();
    }

    private void LoadTodos()
    {
        _allTodos.Clear();

        foreach (var todo in _todoRepository.GetAll())
        {
            _allTodos.Add(TodoItemViewModel.From(todo));
        }

        ActiveCount = _allTodos.Count(todo => !todo.IsCompleted);
        CompletedCount = _allTodos.Count - ActiveCount;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<TodoItemViewModel> filteredTodos = SelectedFilter switch
        {
            TodoFilter.Active => _allTodos.Where(todo => !todo.IsCompleted),
            TodoFilter.Completed => _allTodos.Where(todo => todo.IsCompleted),
            _ => _allTodos,
        };

        VisibleTodos.Clear();
        foreach (var todo in filteredTodos)
        {
            VisibleTodos.Add(todo);
        }
    }
}
