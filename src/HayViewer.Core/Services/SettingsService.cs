using System.Text.Json;
using HayViewer.Core.Models;

namespace HayViewer.Core.Services;

public class SettingsService
{
    private static readonly string SettingsFile = Path.Combine(
        AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return new AppSettings();
            string json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json);
        }
        catch { /* best-effort; never crash on settings save */ }
    }

    public void AddRecentFile(AppSettings settings, string path)
    {
        settings.RecentFiles.Remove(path);
        settings.RecentFiles.Insert(0, path);
        while (settings.RecentFiles.Count > AppSettings.MaxRecentFiles)
            settings.RecentFiles.RemoveAt(settings.RecentFiles.Count - 1);
    }
}
