using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using TodoList.App.Data;
using TodoList.App.Models;
using TodoList.App.ViewModels;
using TodoList.App.Views;

namespace TodoList.App;

public partial class App : Application
{
    private static readonly TimeSpan SettingsSaveDebounceDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan TrayClickReopenSuppressionWindow = TimeSpan.FromMilliseconds(300);

    private static readonly string[] AvailableGroupingOptions =
    [
        "None",
        "Added day",
        "Due date",
        "Priority",
    ];

    private static readonly string[] AvailableOrderingOptions =
    [
        "Creation date",
        "Due date",
        "Priority",
    ];

    private static readonly string[] AvailableOrderingDirections =
    [
        "Descending",
        "Ascending",
    ];

    private MainWindow? _mainWindow;
    private MainWindowViewModel? _mainWindowViewModel;
    private JsonAppSettingsStore? _settingsStore;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private string? _databasePath;
    private AppUiSettings _initialSettings = new();
    private EventHandler<AvaloniaPropertyChangedEventArgs>? _mainWindowPropertyChangedHandler;
    private PropertyChangedEventHandler? _viewModelPropertyChangedHandler;
    private DispatcherTimer? _settingsSaveDebounceTimer;
    private bool _hasPendingSettingsSave;
    private bool _isExitRequested;
    private DateTimeOffset _lastMainWindowClosedAtUtc = DateTimeOffset.MinValue;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _desktop = desktop;

            var dataDirectory = BuildDataDirectory();
            _databasePath = Path.Combine(dataDirectory, "todos.sqlite");
            var settingsPath = Path.Combine(dataDirectory, "settings.json");

            _settingsStore = new JsonAppSettingsStore(settingsPath);
            _initialSettings = SanitizeSettings(_settingsStore.Load());

            // Persist sanitized defaults even before the first panel open.
            _settingsStore.Save(_initialSettings);

            _settingsSaveDebounceTimer = new DispatcherTimer
            {
                Interval = SettingsSaveDebounceDelay,
            };
            _settingsSaveDebounceTimer.Tick += SettingsSaveDebounceTimer_OnTick;

            desktop.Exit += (_, _) => FlushPendingSettingsSnapshot(forceSaveCurrentSnapshot: true);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplySettingsToViewModel(
        MainWindowViewModel viewModel,
        AppUiSettings settings)
    {
        viewModel.IsPinned = settings.IsPinned;
        viewModel.SelectedFilter = settings.SelectedFilter;
        viewModel.SelectedSmartView = settings.SelectedSmartView;
        viewModel.SelectedPriorityFilter = settings.SelectedPriorityFilter;
        viewModel.SelectedGroupingOption = settings.SelectedGroupingOption;
        viewModel.SelectedGroupingDirection = settings.SelectedGroupingDirection;
        viewModel.SelectedOrderingOption = settings.SelectedOrderingOption;
        viewModel.SelectedOrderingDirection = settings.SelectedOrderingDirection;
    }

    private void EnsureMainWindow()
    {
        if (_mainWindow is not null
            || _settingsStore is null
            || _desktop is null
            || string.IsNullOrWhiteSpace(_databasePath))
        {
            return;
        }

        _mainWindowViewModel = CreateMainWindowViewModel(_databasePath, _initialSettings);

        _mainWindow = new MainWindow
        {
            DataContext = _mainWindowViewModel,
            WidthPercent = _initialSettings.WidthPercent,
        };

        WireSettingsPersistence(_mainWindow, _mainWindowViewModel);

        _mainWindow.Closing += MainWindow_OnClosing;
        _desktop.MainWindow = _mainWindow;
    }

    private static MainWindowViewModel CreateMainWindowViewModel(
        string databasePath,
        AppUiSettings settings)
    {
        var todoRepository = new SqliteTodoRepository(databasePath);
        var viewModel = new MainWindowViewModel(todoRepository);
        ApplySettingsToViewModel(viewModel, settings);
        return viewModel;
    }

    private static string BuildDataDirectory()
    {
        var dataDirectoryName = "TodoListPanel";
    #if DEBUG
        dataDirectoryName += "-debug";
    #endif

        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            dataDirectoryName);

        Directory.CreateDirectory(dataDirectory);

        return dataDirectory;
    }

    private static AppUiSettings SanitizeSettings(AppUiSettings settings)
    {
        var defaultGroupingOption = AvailableGroupingOptions.First();
        var groupingOption = settings.SelectedGroupingOption;
        var defaultGroupingDirection = AvailableOrderingDirections.First();
        var groupingDirection = settings.SelectedGroupingDirection;
        var defaultOrderingOption = AvailableOrderingOptions.First();
        var orderingOption = settings.SelectedOrderingOption;
        var defaultOrderingDirection = AvailableOrderingDirections.First();
        var orderingDirection = settings.SelectedOrderingDirection;

        if (string.IsNullOrWhiteSpace(groupingOption)
            || !AvailableGroupingOptions.Contains(groupingOption))
        {
            groupingOption = defaultGroupingOption;
        }

        if (string.IsNullOrWhiteSpace(orderingOption)
            || !AvailableOrderingOptions.Contains(orderingOption))
        {
            orderingOption = defaultOrderingOption;
        }

        if (string.IsNullOrWhiteSpace(groupingDirection)
            || !AvailableOrderingDirections.Contains(groupingDirection))
        {
            groupingDirection = defaultGroupingDirection;
        }

        if (string.IsNullOrWhiteSpace(orderingDirection)
            || !AvailableOrderingDirections.Contains(orderingDirection))
        {
            orderingDirection = defaultOrderingDirection;
        }

        var selectedFilter = Enum.IsDefined(typeof(TodoFilter), settings.SelectedFilter)
            ? settings.SelectedFilter
            : TodoFilter.Active;

        var selectedPriorityFilter = Enum.IsDefined(typeof(TodoPriorityFilter), settings.SelectedPriorityFilter)
            ? settings.SelectedPriorityFilter
            : TodoPriorityFilter.All;

        var selectedSmartView = Enum.IsDefined(typeof(TodoSmartView), settings.SelectedSmartView)
            ? settings.SelectedSmartView
            : TodoSmartView.None;

        (selectedFilter, selectedPriorityFilter) = NormalizeLegacyCombinedFilter(
            selectedFilter,
            selectedPriorityFilter);

        return new AppUiSettings
        {
            WidthPercent = Math.Clamp(settings.WidthPercent, 80m, 200m),
            IsPinned = settings.IsPinned,
            SelectedFilter = selectedFilter,
            SelectedSmartView = selectedSmartView,
            SelectedPriorityFilter = selectedPriorityFilter,
            SelectedGroupingOption = groupingOption,
            SelectedGroupingDirection = groupingDirection,
            SelectedOrderingOption = orderingOption,
            SelectedOrderingDirection = orderingDirection,
        };
    }

    private static AppUiSettings BuildCurrentSettingsSnapshot(
        MainWindow mainWindow,
        MainWindowViewModel viewModel)
    {
        return new AppUiSettings
        {
            WidthPercent = Math.Clamp(mainWindow.WidthPercent, 80m, 200m),
            IsPinned = viewModel.IsPinned,
            SelectedFilter = viewModel.SelectedFilter,
            SelectedSmartView = viewModel.SelectedSmartView,
            SelectedPriorityFilter = viewModel.SelectedPriorityFilter,
            SelectedGroupingOption = viewModel.SelectedGroupingOption,
            SelectedGroupingDirection = viewModel.SelectedGroupingDirection,
            SelectedOrderingOption = viewModel.SelectedOrderingOption,
            SelectedOrderingDirection = viewModel.SelectedOrderingDirection,
        };
    }

    private void WireSettingsPersistence(
        MainWindow mainWindow,
        MainWindowViewModel viewModel)
    {
        _mainWindowPropertyChangedHandler = (_, e) =>
        {
            if (e.Property == MainWindow.WidthPercentProperty)
            {
                RequestSettingsSnapshotSave();
            }
        };
        mainWindow.PropertyChanged += _mainWindowPropertyChangedHandler;

        _viewModelPropertyChangedHandler = (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.IsPinned)
                or nameof(MainWindowViewModel.SelectedFilter)
                or nameof(MainWindowViewModel.SelectedSmartView)
                or nameof(MainWindowViewModel.SelectedPriorityFilter)
                or nameof(MainWindowViewModel.SelectedGroupingOption)
                or nameof(MainWindowViewModel.SelectedGroupingDirection)
                or nameof(MainWindowViewModel.SelectedOrderingOption)
                or nameof(MainWindowViewModel.SelectedOrderingDirection))
            {
                RequestSettingsSnapshotSave();
            }
        };
        viewModel.PropertyChanged += _viewModelPropertyChangedHandler;

        // Ensure defaults or sanitized values are written even before first user interaction.
        SaveCurrentSettingsSnapshot();
    }

    private void RequestSettingsSnapshotSave()
    {
        if (_settingsStore is null)
        {
            return;
        }

        _hasPendingSettingsSave = true;

        if (_settingsSaveDebounceTimer is null)
        {
            SaveCurrentSettingsSnapshot();
            _hasPendingSettingsSave = false;
            return;
        }

        _settingsSaveDebounceTimer.Stop();
        _settingsSaveDebounceTimer.Start();
    }

    private void SettingsSaveDebounceTimer_OnTick(object? sender, EventArgs e)
    {
        FlushPendingSettingsSnapshot();
    }

    private void FlushPendingSettingsSnapshot(bool forceSaveCurrentSnapshot = false)
    {
        _settingsSaveDebounceTimer?.Stop();

        if (!_hasPendingSettingsSave && !forceSaveCurrentSnapshot)
        {
            return;
        }

        _hasPendingSettingsSave = false;
        SaveCurrentSettingsSnapshot();
    }

    private void SaveCurrentSettingsSnapshot()
    {
        if (_settingsStore is null)
        {
            return;
        }

        if (_mainWindow is not null && _mainWindowViewModel is not null)
        {
            _initialSettings = BuildCurrentSettingsSnapshot(_mainWindow, _mainWindowViewModel);
        }

        _settingsStore.Save(_initialSettings);
    }

    private void DetachMainWindowPersistenceHandlers()
    {
        if (_mainWindow is not null && _mainWindowPropertyChangedHandler is not null)
        {
            _mainWindow.PropertyChanged -= _mainWindowPropertyChangedHandler;
        }

        if (_mainWindowViewModel is not null && _viewModelPropertyChangedHandler is not null)
        {
            _mainWindowViewModel.PropertyChanged -= _viewModelPropertyChangedHandler;
        }

        _mainWindowPropertyChangedHandler = null;
        _viewModelPropertyChangedHandler = null;
    }

    private static (TodoFilter selectedFilter, TodoPriorityFilter selectedPriorityFilter)
        NormalizeLegacyCombinedFilter(
            TodoFilter selectedFilter,
            TodoPriorityFilter selectedPriorityFilter)
    {
        if (selectedPriorityFilter != TodoPriorityFilter.All)
        {
            return (NormalizeStatusFilter(selectedFilter), selectedPriorityFilter);
        }

        return selectedFilter switch
        {
            TodoFilter.Minor => (TodoFilter.All, TodoPriorityFilter.Minor),
            TodoFilter.Normal => (TodoFilter.All, TodoPriorityFilter.Normal),
            TodoFilter.Major => (TodoFilter.All, TodoPriorityFilter.Major),
            TodoFilter.Critical => (TodoFilter.All, TodoPriorityFilter.Critical),
            _ => (NormalizeStatusFilter(selectedFilter), selectedPriorityFilter),
        };
    }

    private static TodoFilter NormalizeStatusFilter(TodoFilter selectedFilter)
    {
        return selectedFilter is TodoFilter.Active
            or TodoFilter.Completed
            or TodoFilter.Rejected
            or TodoFilter.All
            ? selectedFilter
            : TodoFilter.Active;
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
        if (_mainWindow is null
            && DateTimeOffset.UtcNow - _lastMainWindowClosedAtUtc <= TrayClickReopenSuppressionWindow)
        {
            return;
        }

        ToggleMainWindowVisibility();
    }

    private void ToggleTodosMenuItem_OnClick(object? sender, EventArgs e)
    {
        ToggleMainWindowVisibility();
    }

    private void ExitMenuItem_OnClick(object? sender, EventArgs e)
    {
        _isExitRequested = true;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExitRequested)
        {
            FlushPendingSettingsSnapshot(forceSaveCurrentSnapshot: true);
            return;
        }

        FlushPendingSettingsSnapshot(forceSaveCurrentSnapshot: true);
        DetachMainWindowPersistenceHandlers();

        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= MainWindow_OnClosing;
        }

        if (_desktop is not null && ReferenceEquals(_desktop.MainWindow, _mainWindow))
        {
            _desktop.MainWindow = null;
        }

        _mainWindow = null;
        _mainWindowViewModel = null;
        _lastMainWindowClosedAtUtc = DateTimeOffset.UtcNow;
    }

    private void ToggleMainWindowVisibility()
    {
        EnsureMainWindow();

        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Close();
            return;
        }

        _mainWindow.ShowPanel();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}