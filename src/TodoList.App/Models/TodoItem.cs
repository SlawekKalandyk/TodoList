using System;

namespace TodoList.App.Models;

public sealed class TodoItem
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsRejected { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }
}
