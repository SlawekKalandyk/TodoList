namespace TodoList.App.Models;

public sealed class TodoStatusCounts
{
    public int ActiveCount { get; init; }

    public int CompletedCount { get; init; }

    public int RejectedCount { get; init; }
}