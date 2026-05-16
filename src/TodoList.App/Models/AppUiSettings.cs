namespace TodoList.App.Models;

public sealed class AppUiSettings
{
    public decimal WidthPercent { get; set; } = 100m;

    public bool IsPinned { get; set; }

    public TodoFilter SelectedFilter { get; set; } = TodoFilter.Active;

    public string SelectedGroupingOption { get; set; } = "None";
}