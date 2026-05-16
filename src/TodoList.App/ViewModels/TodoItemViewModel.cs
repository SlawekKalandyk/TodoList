using CommunityToolkit.Mvvm.ComponentModel;
using System;
using TodoList.App.Models;

namespace TodoList.App.ViewModels;

public sealed partial class TodoItemViewModel : ObservableObject
{
    public long Id { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public TodoPriority Priority { get; }

    public string PriorityLabel => Priority.ToString();

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isRejected;

    [ObservableProperty]
    private bool isRenaming;

    [ObservableProperty]
    private string renameText;

    public bool CanReject => !IsRejected;

    public bool IsNotRenaming => !IsRenaming;

    public TodoItemViewModel(
        long id,
        string title,
        TodoPriority priority,
        bool isCompleted,
        bool isRejected,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        Priority = priority;
        this.title = title;
        this.isCompleted = isCompleted;
        this.isRejected = isRejected;
        renameText = title;
    }

    public static TodoItemViewModel From(TodoItem todo)
    {
        return new TodoItemViewModel(
            todo.Id,
            todo.Title,
            todo.Priority,
            todo.IsCompleted,
            todo.IsRejected,
            todo.CreatedAtUtc);
    }

    partial void OnIsRejectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanReject));
    }

    partial void OnIsRenamingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotRenaming));
    }

    partial void OnTitleChanged(string value)
    {
        if (!IsRenaming)
        {
            RenameText = value;
        }
    }

    public void BeginRename()
    {
        RenameText = Title;
        IsRenaming = true;
    }

    public void CancelRename()
    {
        RenameText = Title;
        IsRenaming = false;
    }
}
