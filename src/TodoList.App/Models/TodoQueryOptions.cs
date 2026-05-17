namespace TodoList.App.Models;

public sealed class TodoQueryOptions
{
    public TodoSmartView SmartView { get; init; } = TodoSmartView.None;

    public TodoFilter StatusFilter { get; init; } = TodoFilter.All;

    public TodoPriorityFilter PriorityFilter { get; init; } = TodoPriorityFilter.All;

    public string SearchQuery { get; init; } = string.Empty;

    public bool IncludeNotesInSearch { get; init; }

    public TodoOrdering Ordering { get; init; } = TodoOrdering.CreationDate;

    public bool OrderAscending { get; init; }

    public long TodayStartUtcUnix { get; init; }

    public long TomorrowStartUtcUnix { get; init; }

    public long DueSoonEndExclusiveUtcUnix { get; init; }
}