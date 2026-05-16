using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TodoList.App.ViewModels;

namespace TodoList.App.Views;

public partial class MainWindow : Window
{
    private const double BasePanelWidth = 600;

    public static readonly StyledProperty<decimal> WidthPercentProperty =
        AvaloniaProperty.Register<MainWindow, decimal>(nameof(WidthPercent), 100m);

    public decimal WidthPercent
    {
        get => GetValue(WidthPercentProperty);
        set => SetValue(WidthPercentProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WidthPercentProperty && IsVisible)
            {
                SnapToRightEdge();
            }
        };

        Opened += (_, _) => SnapToRightEdge();
        Deactivated += (_, _) =>
        {
            if (IsVisible && ShouldAutoHide())
            {
                Hide();
            }
        };
    }

    public void ShowPanel()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        SnapToRightEdge();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
    }

    private void SnapToRightEdge()
    {
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;
        var targetScreen = ResolveTargetScreen(scaling);
        if (targetScreen is null)
        {
            return;
        }

        var targetScaling = targetScreen.Scaling <= 0 ? scaling : targetScreen.Scaling;
        var workArea = targetScreen.WorkingArea;
        var percent = Math.Clamp(WidthPercent, 50m, 200m);

        if (percent != WidthPercent)
        {
            SetCurrentValue(WidthPercentProperty, percent);
        }

        var desiredPanelWidth = BasePanelWidth * (double)(percent / 100m);
        var panelWidth = CoerceWidthToBounds(desiredPanelWidth);
        var panelWidthPixels = (int)Math.Round(panelWidth * targetScaling);

        Width = panelWidth;
        Height = workArea.Height / targetScaling;
        Position = new PixelPoint(
            workArea.X + workArea.Width - panelWidthPixels,
            workArea.Y);
    }

    private double CoerceWidthToBounds(double width)
    {
        var min = double.IsNaN(MinWidth) ? 0 : MinWidth;
        var max = double.IsNaN(MaxWidth) || double.IsPositiveInfinity(MaxWidth)
            ? double.PositiveInfinity
            : MaxWidth;

        if (width < min)
        {
            return min;
        }

        if (width > max)
        {
            return max;
        }

        return width;
    }

    private Screen? ResolveTargetScreen(double scaling)
    {
        var currentWidth = Width;
        if (double.IsNaN(currentWidth) || currentWidth <= 0)
        {
            currentWidth = BasePanelWidth;
        }

        var rightEdgePoint = new PixelPoint(
            Position.X + (int)Math.Round(currentWidth * scaling) - 1,
            Position.Y + 1);

        return Screens.ScreenFromPoint(rightEdgePoint)
            ?? Screens.ScreenFromWindow(this)
            ?? Screens.Primary;
    }

    private bool ShouldAutoHide()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            return !viewModel.IsPinned;
        }

        return true;
    }

    private void TodoItemRow_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control row
            || row.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        if (e.Source is Visual sourceVisual
            && (sourceVisual is Button
                || sourceVisual is CheckBox
                || sourceVisual is TextBox
                || sourceVisual.FindAncestorOfType<Button>() is not null
                || sourceVisual.FindAncestorOfType<CheckBox>() is not null
                || sourceVisual.FindAncestorOfType<TextBox>() is not null))
        {
            return;
        }

        viewModel.StartRenameTodoCommand.Execute(todo);

        Dispatcher.UIThread.Post(() =>
        {
            var renameTextBox = row
                .GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(control => control.IsVisible);

            if (renameTextBox is null)
            {
                return;
            }

            renameTextBox.Focus();
            renameTextBox.SelectAll();
        }, DispatcherPriority.Input);

        e.Handled = true;
    }

    private void TodoRenameTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control control
            || control.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        viewModel.CommitRenameTodoCommand.Execute(todo);
    }
}