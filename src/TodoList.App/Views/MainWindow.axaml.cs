using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TodoList.App.Models;
using TodoList.App.ViewModels;

namespace TodoList.App.Views;

public partial class MainWindow : Window
{
    private const double BasePanelWidth = 600;
    private const decimal WidthPercentMinimum = 80m;
    private const decimal WidthPercentMaximum = 200m;

    private bool _isTodoPriorityDropDownOpen;

    public static readonly StyledProperty<decimal> WidthPercentProperty =
        AvaloniaProperty.Register<MainWindow, decimal>(nameof(WidthPercent), 100m);

    public static readonly StyledProperty<decimal?> WidthPercentEditorValueProperty =
        AvaloniaProperty.Register<MainWindow, decimal?>(nameof(WidthPercentEditorValue), 100m);

    public decimal WidthPercent
    {
        get => GetValue(WidthPercentProperty);
        set => SetValue(WidthPercentProperty, value);
    }

    public decimal? WidthPercentEditorValue
    {
        get => GetValue(WidthPercentEditorValueProperty);
        set => SetValue(WidthPercentEditorValueProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, MainWindow_OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(TappedEvent, MainWindow_OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);

        PropertyChanged += (_, e) =>
        {
            if (e.Property == WidthPercentEditorValueProperty)
            {
                if (WidthPercentEditorValue is decimal widthPercentEditorValue)
                {
                    if (ShouldDeferOutOfRangeWidthUpdate(widthPercentEditorValue))
                    {
                        return;
                    }

                    SetCurrentValue(
                        WidthPercentProperty,
                        Math.Clamp(widthPercentEditorValue, WidthPercentMinimum, WidthPercentMaximum));
                }

                return;
            }

            if (e.Property == WidthPercentProperty)
            {
                if (WidthPercentEditorValue != WidthPercent)
                {
                    SetCurrentValue(WidthPercentEditorValueProperty, WidthPercent);
                }

                if (IsVisible)
                {
                    SnapToRightEdge();
                }
            }
        };

        Opened += (_, _) => SnapToRightEdge();
        Deactivated += (_, _) =>
        {
            if (IsVisible && ShouldAutoHide())
            {
                Close();
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

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            FocusSearchTextBox();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape
            && SearchTodoTextBox is not null
            && SearchTodoTextBox.IsKeyboardFocusWithin
            && DataContext is MainWindowViewModel viewModel
            && !string.IsNullOrWhiteSpace(viewModel.SearchQuery))
        {
            viewModel.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void FocusSearchTextBox()
    {
        if (SearchTodoTextBox is null)
        {
            return;
        }

        SearchTodoTextBox.Focus();
        SearchTodoTextBox.SelectAll();
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
        var percent = Math.Clamp(WidthPercent, WidthPercentMinimum, WidthPercentMaximum);

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

    private void TodoRenameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control renameButton
            || renameButton.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        viewModel.StartRenameTodoCommand.Execute(todo);

        Dispatcher.UIThread.Post(() =>
        {
            var row = renameButton.FindAncestorOfType<Border>();
            if (row is null)
            {
                return;
            }

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

    private void TodoItemRow_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control row
            || row.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        if (IsTapWithinVisibleTodoDetailsPanel(e))
        {
            e.Handled = true;
            return;
        }

        if (_isTodoPriorityDropDownOpen)
        {
            e.Handled = true;
            return;
        }

        if (e.Source is Visual sourceVisual
            && (IsWithinClass(sourceVisual, "todoDetailsPanel")
                || sourceVisual is Button
                || sourceVisual is CheckBox
                || sourceVisual is ComboBox
                || sourceVisual is TextBox
                || sourceVisual.FindAncestorOfType<Button>() is not null
                || sourceVisual.FindAncestorOfType<CheckBox>() is not null
                || sourceVisual.FindAncestorOfType<ComboBox>() is not null
                || IsWithinDatePicker(sourceVisual)
                || sourceVisual.FindAncestorOfType<TextBox>() is not null))
        {
            return;
        }

        viewModel.ToggleTodoDetailsCommand.Execute(todo);
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

    private void WidthPercentInput_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        NormalizeWidthPercentInputValue(sender as NumericUpDown);
    }

    private void TodoNotesTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control control
            || control.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        viewModel.SaveTodoNotesCommand.Execute(todo);
    }

    private void TodoDueDatePicker_OnSelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs _)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not DatePicker datePicker
            || datePicker.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        viewModel.SaveTodoDueAtUtcCommand.Execute(todo);
    }

    private void TodoPriorityComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not ComboBox comboBox
            || (!comboBox.IsDropDownOpen && !comboBox.IsKeyboardFocusWithin)
            || comboBox.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        viewModel.SaveTodoPriorityCommand.Execute(todo);
    }

    private void TodoPriorityComboBox_OnDropDownOpened(object? sender, EventArgs e)
    {
        _isTodoPriorityDropDownOpen = true;
    }

    private void TodoPriorityComboBox_OnDropDownClosed(object? sender, EventArgs e)
    {
        _isTodoPriorityDropDownOpen = false;
    }

    private void TodoListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedIndex < 0)
        {
            return;
        }

        listBox.SelectedIndex = -1;
    }

    private void MainWindow_OnTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || e.Source is not Visual sourceVisual)
        {
            return;
        }

        if (!IsWithinTextBox(sourceVisual)
            && !IsWithinClass(sourceVisual, "renameTodoButton"))
        {
            viewModel.CommitActiveRename();
        }

        TryBlurWidthPercentInput(sourceVisual);

        var isWithinComboBox = IsWithinComboBox(sourceVisual);
        var isWithinDatePicker = IsWithinDatePicker(sourceVisual);
        var isWithinDetailsPanelByPosition = IsTapWithinVisibleTodoDetailsPanel(e);

        var isWithinTodoUi = IsWithinClass(sourceVisual, "todoDetailsPanel")
            || IsWithinClass(sourceVisual, "todoRow")
            || isWithinComboBox
            || isWithinDatePicker;

        if (_isTodoPriorityDropDownOpen && !isWithinComboBox)
        {
            return;
        }

        if (isWithinDetailsPanelByPosition)
        {
            return;
        }

        if (isWithinTodoUi)
        {
            return;
        }

        viewModel.CollapseTodoDetailsCommand.Execute(null);
    }

    private void MainWindow_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual sourceVisual)
        {
            return;
        }

        TryCommitWidthPercentInputBeforeFocusChange(sourceVisual);
    }

    private void NormalizeWidthPercentInputValue(NumericUpDown? widthInput = null)
    {
        var rawValue = TryReadWidthPercentInputTextValue(widthInput)
            ?? widthInput?.Value
            ?? WidthPercentEditorValue
            ?? WidthPercent;

        var normalizedValue = Math.Clamp(rawValue, WidthPercentMinimum, WidthPercentMaximum);
        var widthChanged = WidthPercent != normalizedValue;

        if (widthChanged)
        {
            SetCurrentValue(WidthPercentProperty, normalizedValue);
        }

        SetCurrentValue(WidthPercentEditorValueProperty, normalizedValue);

        if (IsVisible && !widthChanged)
        {
            SnapToRightEdge();
        }
    }

    private void TryBlurWidthPercentInput(Visual sourceVisual)
    {
        if (IsWithinNumericUpDown(sourceVisual)
            || WidthPercentInput is null
            || !WidthPercentInput.IsKeyboardFocusWithin)
        {
            return;
        }

        NormalizeWidthPercentInputValue(WidthPercentInput);

        if (IsWithinFocusableControl(sourceVisual))
        {
            return;
        }

        PanelRoot.Focus();
    }

    private bool ShouldDeferOutOfRangeWidthUpdate(decimal widthPercentEditorValue)
    {
        if (WidthPercentInput is null || !WidthPercentInput.IsKeyboardFocusWithin)
        {
            return false;
        }

        var typedValue = TryReadWidthPercentInputTextValue(WidthPercentInput);
        if (typedValue is decimal widthPercentTypedValue)
        {
            return widthPercentTypedValue < WidthPercentMinimum
                || widthPercentTypedValue > WidthPercentMaximum;
        }

        return widthPercentEditorValue < WidthPercentMinimum
            || widthPercentEditorValue > WidthPercentMaximum;
    }

    private void TryCommitWidthPercentInputBeforeFocusChange(Visual sourceVisual)
    {
        if (IsWithinNumericUpDown(sourceVisual)
            || WidthPercentInput is null
            || !WidthPercentInput.IsKeyboardFocusWithin)
        {
            return;
        }

        NormalizeWidthPercentInputValue(WidthPercentInput);
    }

    private static bool IsWithinClass(Visual sourceVisual, string className)
    {
        Visual? current = sourceVisual;

        while (current is not null)
        {
            if (current is StyledElement styledElement && styledElement.Classes.Contains(className))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private static bool IsWithinTextBox(Visual sourceVisual)
    {
        return sourceVisual is TextBox || sourceVisual.FindAncestorOfType<TextBox>() is not null;
    }

    private static bool IsWithinNumericUpDown(Visual sourceVisual)
    {
        return sourceVisual is NumericUpDown || sourceVisual.FindAncestorOfType<NumericUpDown>() is not null;
    }

    private static bool IsWithinComboBox(Visual sourceVisual)
    {
        return sourceVisual is ComboBox
            || sourceVisual is ComboBoxItem
            || sourceVisual.FindAncestorOfType<ComboBox>() is not null
            || sourceVisual.FindAncestorOfType<ComboBoxItem>() is not null;
    }

    private static bool IsWithinDatePicker(Visual sourceVisual)
    {
        Visual? current = sourceVisual;

        while (current is not null)
        {
            var typeName = current.GetType().Name;
            if (typeName.Contains("DatePicker", StringComparison.OrdinalIgnoreCase)
                || typeName.Contains("Calendar", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private static decimal? TryReadWidthPercentInputTextValue(NumericUpDown? widthInput)
    {
        if (widthInput is null)
        {
            return null;
        }

        var text = widthInput
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(control => control.IsVisible)
            ?.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedValue)
            ? parsedValue
            : null;
    }

    private static bool IsWithinFocusableControl(Visual sourceVisual)
    {
        Visual? current = sourceVisual;

        while (current is not null)
        {
            if (current is InputElement inputElement && inputElement.Focusable)
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    private bool IsTapWithinVisibleTodoDetailsPanel(TappedEventArgs tappedEventArgs)
    {
        var tapPosition = tappedEventArgs.GetPosition(this);

        foreach (var detailsPanel in this.GetVisualDescendants().OfType<Border>())
        {
            if (!detailsPanel.IsVisible || !detailsPanel.Classes.Contains("todoDetailsPanel"))
            {
                continue;
            }

            var detailsPanelTopLeft = detailsPanel.TranslatePoint(default, this);
            if (detailsPanelTopLeft is null)
            {
                continue;
            }

            var detailsPanelBounds = new Rect(detailsPanelTopLeft.Value, detailsPanel.Bounds.Size);
            if (detailsPanelBounds.Contains(tapPosition))
            {
                return true;
            }
        }

        return false;
    }
}
