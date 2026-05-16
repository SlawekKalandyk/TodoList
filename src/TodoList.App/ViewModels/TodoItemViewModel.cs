using CommunityToolkit.Mvvm.ComponentModel;
using TodoList.App.Models;

namespace TodoList.App.ViewModels;

public sealed partial class TodoItemViewModel : ObservableObject
{
    public long Id { get; }

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isRejected;

    public bool CanReject => !IsRejected;

    public TodoItemViewModel(long id, string title, bool isCompleted, bool isRejected)
    {
        Id = id;
        this.title = title;
        this.isCompleted = isCompleted;
        this.isRejected = isRejected;
    }

    public static TodoItemViewModel From(TodoItem todo)
    {
        return new TodoItemViewModel(todo.Id, todo.Title, todo.IsCompleted, todo.IsRejected);
    }

    partial void OnIsRejectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanReject));
    }
}
