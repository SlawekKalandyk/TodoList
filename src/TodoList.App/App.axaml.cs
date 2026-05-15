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

            var databasePath = BuildDatabasePath();
            var todoRepository = new SqliteTodoRepository(databasePath);
            var mainWindowViewModel = new MainWindowViewModel(todoRepository);

            _mainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel,
            };

            _mainWindow.Closing += MainWindow_OnClosing;
            desktop.MainWindow = _mainWindow;

            // Start in the tray and only show the panel when requested.
            desktop.Startup += (_, _) => _mainWindow.Hide();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string BuildDatabasePath()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TodoListPanel");

        Directory.CreateDirectory(dataDirectory);

        return Path.Combine(dataDirectory, "todos.sqlite");
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