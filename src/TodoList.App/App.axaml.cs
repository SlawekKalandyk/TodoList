using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.IO;
using System.Linq;
using TodoList.App.Data;
using TodoList.App.Models;
using TodoList.App.ViewModels;
using TodoList.App.Views;

namespace TodoList.App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private bool _isExitRequested;

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

            var dataDirectory = BuildDataDirectory();
            var databasePath = Path.Combine(dataDirectory, "todos.sqlite");
            var settingsPath = Path.Combine(dataDirectory, "settings.json");

            var todoRepository = new SqliteTodoRepository(databasePath);
            var settingsStore = new JsonAppSettingsStore(settingsPath);
            var mainWindowViewModel = new MainWindowViewModel(todoRepository);
            var appSettings = SanitizeSettings(settingsStore.Load(), mainWindowViewModel);

            _mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
                WidthPercent = appSettings.WidthPercent,
            };

            mainWindowViewModel.IsPinned = appSettings.IsPinned;
            mainWindowViewModel.SelectedFilter = appSettings.SelectedFilter;
            mainWindowViewModel.SelectedGroupingOption = appSettings.SelectedGroupingOption;

            WireSettingsPersistence(_mainWindow, mainWindowViewModel, settingsStore, desktop);

            _mainWindow.Closing += MainWindow_OnClosing;
            desktop.MainWindow = _mainWindow;

            // Start in the tray and only show the panel when requested.
            desktop.Startup += (_, _) => _mainWindow.Hide();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string BuildDataDirectory()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoListPanel");

        Directory.CreateDirectory(dataDirectory);

        return dataDirectory;
    }

    private static AppUiSettings SanitizeSettings(
        AppUiSettings settings,
        MainWindowViewModel viewModel)
    {
        var defaultGroupingOption = viewModel.AvailableGroupingOptions.FirstOrDefault() ?? "None";
        var groupingOption = settings.SelectedGroupingOption;

        if (string.IsNullOrWhiteSpace(groupingOption)
            || !viewModel.AvailableGroupingOptions.Contains(groupingOption))
        {
            groupingOption = defaultGroupingOption;
        }

        return new AppUiSettings
        {
            WidthPercent = Math.Clamp(settings.WidthPercent, 50m, 200m),
            IsPinned = settings.IsPinned,
            SelectedFilter = Enum.IsDefined(typeof(TodoFilter), settings.SelectedFilter)
                ? settings.SelectedFilter
                : TodoFilter.Active,
            SelectedGroupingOption = groupingOption,
        };
    }

    private static AppUiSettings BuildCurrentSettingsSnapshot(
        MainWindow mainWindow,
        MainWindowViewModel viewModel)
    {
        return new AppUiSettings
        {
            WidthPercent = Math.Clamp(mainWindow.WidthPercent, 50m, 200m),
            IsPinned = viewModel.IsPinned,
            SelectedFilter = viewModel.SelectedFilter,
            SelectedGroupingOption = viewModel.SelectedGroupingOption,
        };
    }

    private static void WireSettingsPersistence(
        MainWindow mainWindow,
        MainWindowViewModel viewModel,
        JsonAppSettingsStore settingsStore,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        void SaveCurrentSettings()
        {
            settingsStore.Save(BuildCurrentSettingsSnapshot(mainWindow, viewModel));
        }

        mainWindow.PropertyChanged += (_, e) =>
        {
            if (e.Property == MainWindow.WidthPercentProperty)
            {
                SaveCurrentSettings();
            }
        };

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainWindowViewModel.IsPinned)
                or nameof(MainWindowViewModel.SelectedFilter)
                or nameof(MainWindowViewModel.SelectedGroupingOption))
            {
                SaveCurrentSettings();
            }
        };

        desktop.Exit += (_, _) => SaveCurrentSettings();

        // Ensure defaults or sanitized values are written even before first user interaction.
        SaveCurrentSettings();
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs e)
    {
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
            return;
        }

        e.Cancel = true;

        if (sender is Window window)
        {
            window.Hide();
        }
    }

    private void ToggleMainWindowVisibility()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
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