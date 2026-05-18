using CommunityToolkit.Mvvm.ComponentModel;
using System;
using TodoList.App.Models;

namespace TodoList.App.ViewModels;

public sealed partial class TodoItemViewModel : ObservableObject
{
    public long Id { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    [ObservableProperty]
    private TodoPriority priority;

    public string PriorityLabel => Priority.ToString();

    public bool IsPriorityMinor => Priority == TodoPriority.Minor;

    public bool IsPriorityNormal => Priority == TodoPriority.Normal;

    public bool IsPriorityMajor => Priority == TodoPriority.Major;

    public bool IsPriorityCritical => Priority == TodoPriority.Critical;

    [ObservableProperty]
    private DateTimeOffset? dueAtUtc;

    public bool HasDueDate => DueAtUtc.HasValue;

    public string DueDateLabel => DueAtUtc?.ToLocalTime().ToString("dd MMM yyyy") ?? string.Empty;

    public string CreatedAtLabel => CreatedAtUtc.ToLocalTime().ToString("dd MMM yyyy HH:mm");

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private string notes;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isRejected;

    [ObservableProperty]
    private bool isRenaming;

    [ObservableProperty]
    private string renameText;

    [ObservableProperty]
    private bool isDetailsExpanded;

    public bool CanToggleCompleted => !IsRejected;

    public bool CanReject => !IsRejected;

    public bool IsNotRenaming => !IsRenaming;

    public TodoItemViewModel(
        long id,
        string title,
        string notes,
        TodoPriority priority,
        DateTimeOffset? dueAtUtc,
        bool isCompleted,
        bool isRejected,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
        this.priority = priority;
        this.dueAtUtc = dueAtUtc;
        this.title = title;
        this.notes = notes ?? string.Empty;
        this.isCompleted = isCompleted;
        this.isRejected = isRejected;
        renameText = title;
    }

    public static TodoItemViewModel From(TodoItem todo)
    {
        return new TodoItemViewModel(
            todo.Id,
            todo.Title,
            todo.Notes,
            todo.Priority,
            todo.DueAtUtc?.ToLocalTime(),
            todo.IsCompleted,
            todo.IsRejected,
            todo.CreatedAtUtc);
    }

    partial void OnIsRejectedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanToggleCompleted));
        OnPropertyChanged(nameof(CanReject));
    }

    partial void OnIsRenamingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotRenaming));
    }

    partial void OnPriorityChanged(TodoPriority value)
    {
        OnPropertyChanged(nameof(PriorityLabel));
        OnPropertyChanged(nameof(IsPriorityMinor));
        OnPropertyChanged(nameof(IsPriorityNormal));
        OnPropertyChanged(nameof(IsPriorityMajor));
        OnPropertyChanged(nameof(IsPriorityCritical));
    }

    partial void OnDueAtUtcChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(HasDueDate));
        OnPropertyChanged(nameof(DueDateLabel));
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
