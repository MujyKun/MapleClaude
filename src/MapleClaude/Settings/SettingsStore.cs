using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MapleClaude.Settings;

/// <summary>
/// Loads and saves <see cref="UserSettings"/> as JSON under
/// <c>%APPDATA%/MapleClaude/settings.json</c>. Both operations are best-effort:
/// a missing or corrupt file yields defaults rather than throwing, so a bad
/// settings file can never stop the client from launching.
/// </summary>
public sealed class SettingsStore
{
    private readonly ILogger<SettingsStore>? _logger;

    /// <summary>The resolved settings file path.</summary>
    public string FilePath { get; }

    public SettingsStore(ILogger<SettingsStore>? logger = null, string? overridePath = null)
    {
        _logger = logger;
        FilePath = overridePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MapleClaude", "settings.json");
    }

    public UserSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new UserSettings();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize(json, UserSettingsJsonContext.Default.UserSettings)
                   ?? new UserSettings();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load settings from {Path}; using defaults", FilePath);
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, UserSettingsJsonContext.Default.UserSettings);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save settings to {Path}", FilePath);
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(UserSettings))]
internal sealed partial class UserSettingsJsonContext : JsonSerializerContext;
