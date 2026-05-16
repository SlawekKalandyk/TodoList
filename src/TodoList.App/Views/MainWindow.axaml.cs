using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
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
}