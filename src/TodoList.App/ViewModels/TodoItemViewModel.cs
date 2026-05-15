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

    public TodoItemViewModel(long id, string title, bool isCompleted)
    {
        Id = id;
        this.title = title;
        this.isCompleted = isCompleted;
    }

    public static TodoItemViewModel From(TodoItem todo)
    {
        return new TodoItemViewModel(todo.Id, todo.Title, todo.IsCompleted);
    }
}
