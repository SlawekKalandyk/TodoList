using System;
using Avalonia;
using Avalonia.Controls;
using TodoList.App.ViewModels;

namespace TodoList.App.Views;

public partial class MainWindow : Window
{
    private const double PanelWidth = 600;

    public MainWindow()
    {
        InitializeComponent();
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
        var targetScreen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (targetScreen is null)
        {
            return;
        }

        var workArea = targetScreen.WorkingArea;
        var scaling = RenderScaling <= 0 ? 1 : RenderScaling;

        Width = PanelWidth;
        Height = workArea.Height / scaling;
        Position = new PixelPoint(
            workArea.X + workArea.Width - (int)Math.Round(PanelWidth * scaling),
            workArea.Y);
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