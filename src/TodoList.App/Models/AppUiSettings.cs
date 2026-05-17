namespace TodoList.App.Models;

public sealed class AppUiSettings
{
    public decimal WidthPercent { get; set; } = 100m;

    public bool IsPinned { get; set; }

    public TodoFilter SelectedFilter { get; set; } = TodoFilter.Active;

    public TodoSmartView SelectedSmartView { get; set; } = TodoSmartView.None;

    public TodoPriorityFilter SelectedPriorityFilter { get; set; } = TodoPriorityFilter.All;

    public string SelectedGroupingOption { get; set; } = "None";

    public string SelectedOrderingOption { get; set; } = "None";

    public string SelectedOrderingDirection { get; set; } = "Descending";
}