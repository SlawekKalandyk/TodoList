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
    private static readonly TimeSpan OverflowMenuReopenSuppressionWindow = TimeSpan.FromMilliseconds(250);

    private bool _isTodoPriorityDropDownOpen;
    private Button? _lastClosedOverflowButton;
    private DateTimeOffset _lastClosedOverflowAtUtc = DateTimeOffset.MinValue;

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
        DeleteConfirmationOverlay.PropertyChanged += DeleteConfirmationOverlay_OnPropertyChanged;
        AddHandler(KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, MainWindow_OnKeyDownBubble, RoutingStrategies.Bubble);
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

        if (e.Key == Key.N
            && e.KeyModifiers.HasFlag(KeyModifiers.Control)
            && DataContext is MainWindowViewModel addComposerViewModel)
        {
            OpenAddComposer(addComposerViewModel);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape
            && DataContext is MainWindowViewModel cancelComposerViewModel
            && cancelComposerViewModel.IsAddComposerOpen)
        {
            cancelComposerViewModel.CancelAddComposerCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape
            && DataContext is MainWindowViewModel cancelDeleteViewModel
            && cancelDeleteViewModel.IsDeleteConfirmationOpen)
        {
            cancelDeleteViewModel.CancelDeleteTodoCommand.Execute(null);
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
            return;
        }

        if (e.Key == Key.F2
            && DataContext is MainWindowViewModel renameViewModel
            && TryStartRenameForFocusedTodo(renameViewModel))
        {
            e.Handled = true;
            return;
        }

    }

    private void MainWindow_OnKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel)
        {
            return;
        }

        Close();
        e.Handled = true;
    }

    private void AddDetailsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        OpenAddComposer(viewModel);
        e.Handled = true;
    }

    private void OpenAddComposer(MainWindowViewModel viewModel)
    {
        viewModel.OpenAddComposerCommand.Execute(null);
        FocusAddComposerTitleTextBox();
    }

    private void FocusAddComposerTitleTextBox()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (AddComposerTitleTextBox is null || !AddComposerTitleTextBox.IsVisible)
            {
                return;
            }

            AddComposerTitleTextBox.Focus();
            AddComposerTitleTextBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void SaveAndAddAnotherButton_OnClick(object? sender, RoutedEventArgs e)
    {
        FocusAddComposerTitleTextBox();
    }

    private void DeleteConfirmationOverlay_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty
            && DeleteConfirmationOverlay.IsVisible)
        {
            FocusDeleteConfirmationCancelButton();
        }
    }

    private void FocusDeleteConfirmationCancelButton()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DeleteConfirmationCancelButton is null || !DeleteConfirmationCancelButton.IsVisible)
            {
                return;
            }

            DeleteConfirmationCancelButton.Focus();
        }, DispatcherPriority.Input);
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

        var rowBorder = FindTodoRowAncestor(renameButton);
        if (rowBorder is null)
        {
            return;
        }

        StartRenameAndFocusTextBox(viewModel, todo, rowBorder);
        e.Handled = true;
    }

    private bool TryStartRenameForFocusedTodo(MainWindowViewModel viewModel)
    {
        var focused = FocusManager?.GetFocusedElement() as Visual;
        if (focused is null)
        {
            return false;
        }

        var rowBorder = FindFocusedTodoRow(focused);
        if (rowBorder?.DataContext is not TodoItemViewModel todo)
        {
            return false;
        }

        if (todo.IsRenaming)
        {
            return true;
        }

        StartRenameAndFocusTextBox(viewModel, todo, rowBorder);
        return true;
    }

    private static void StartRenameAndFocusTextBox(
        MainWindowViewModel viewModel,
        TodoItemViewModel todo,
        Border rowBorder)
    {
        viewModel.StartRenameTodoCommand.Execute(todo);

        Dispatcher.UIThread.Post(() =>
        {
            var renameTextBox = rowBorder
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
    }

    private static Border? FindTodoRowAncestor(Visual visual)
    {
        Visual? current = visual;
        while (current is not null)
        {
            if (current is Border border && border.Classes.Contains("todoRow"))
            {
                return border;
            }
            current = current.GetVisualParent();
        }
        return null;
    }

    private static Border? FindFocusedTodoRow(Visual focused)
    {
        var ancestorRow = FindTodoRowAncestor(focused);
        if (ancestorRow is not null)
        {
            return ancestorRow;
        }

        if (focused is ListBoxItem)
        {
            return focused
                .GetVisualDescendants()
                .OfType<Border>()
                .FirstOrDefault(border => border.Classes.Contains("todoRow"));
        }

        return null;
    }

    private void TodoOverflowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button overflowButton
            || overflowButton.ContextMenu is not ContextMenu contextMenu)
        {
            return;
        }

        contextMenu.Tag = overflowButton;

        var closedRecently = ReferenceEquals(_lastClosedOverflowButton, overflowButton)
            && DateTimeOffset.UtcNow - _lastClosedOverflowAtUtc <= OverflowMenuReopenSuppressionWindow;
        if (closedRecently)
        {
            _lastClosedOverflowButton = null;
            e.Handled = true;
            return;
        }

        if (contextMenu.IsOpen)
        {
            contextMenu.Close();
            e.Handled = true;
            return;
        }

        contextMenu.Open(overflowButton);
        e.Handled = true;
    }

    private void TodoOverflowMenu_OnOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu
            || ResolveOverflowMenuButton(contextMenu) is not Button overflowButton)
        {
            return;
        }

        overflowButton.Classes.Add("menuOpen");
        if (ReferenceEquals(_lastClosedOverflowButton, overflowButton))
        {
            _lastClosedOverflowButton = null;
        }
    }

    private void TodoOverflowMenu_OnClosed(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu
            || ResolveOverflowMenuButton(contextMenu) is not Button overflowButton)
        {
            return;
        }

        overflowButton.Classes.Remove("menuOpen");
        _lastClosedOverflowButton = overflowButton;
        _lastClosedOverflowAtUtc = DateTimeOffset.UtcNow;
    }

    private static Button? ResolveOverflowMenuButton(ContextMenu contextMenu)
    {
        return contextMenu.Tag as Button ?? contextMenu.PlacementTarget as Button;
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

    private void TodoRenameTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not Control control
            || control.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.CommitRenameTodoCommand.Execute(todo);
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.CancelRenameTodoCommand.Execute(todo);
        }
        else
        {
            return;
        }

        var rowBorder = FindTodoRowAncestor(control);
        if (rowBorder is not null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var renameButton = rowBorder
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .FirstOrDefault(button => button.Classes.Contains("renameTodoButton"));
                renameButton?.Focus();
            }, DispatcherPriority.Input);
        }

        e.Handled = true;
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

    private void TodoDueDatePicker_OnSelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || sender is not DatePicker datePicker
            || datePicker.DataContext is not TodoItemViewModel todo)
        {
            return;
        }

        // Ignore rebinding noise when the picker is just reflecting the current model value.
        if (e.NewDate == todo.DueAtUtc)
        {
            return;
        }

        if (!e.NewDate.HasValue || e.NewDate == e.OldDate)
        {
            return;
        }

        todo.DueAtUtc = e.NewDate;

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

        if (viewModel.IsAddComposerOpen || viewModel.IsDeleteConfirmationOpen)
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
