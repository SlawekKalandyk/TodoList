using System;
using Avalonia;
using Avalonia.Controls;

namespace TodoList.App.Views;

public partial class MainWindow : Window
{
    private const double PanelWidth = 380;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => SnapToRightEdge();
        Deactivated += (_, _) =>
        {
            if (IsVisible)
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
}