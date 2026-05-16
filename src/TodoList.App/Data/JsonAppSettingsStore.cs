using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TodoList.App.Models;

namespace TodoList.App.Data;

public sealed class JsonAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsPath;

    public JsonAppSettingsStore(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException("Settings path cannot be empty.", nameof(settingsPath));
        }

        _settingsPath = settingsPath;
    }

    public AppUiSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppUiSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppUiSettings>(json, JsonOptions) ?? new AppUiSettings();
        }
        catch
        {
            return new AppUiSettings();
        }
    }

    public void Save(AppUiSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}