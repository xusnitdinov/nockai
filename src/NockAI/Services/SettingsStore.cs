using System.IO;
using System.Text.Json;

namespace NockAI.Services;

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string SettingsPath =>
        Path.Combine(SecretsStore.AppDataDir, "settings.json");

    public static AppSettings Current { get; private set; } = new();

    public static event Action? Changed;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                Current = new AppSettings();
            }
        }
        catch
        {
            Current = new AppSettings();
        }

        Current.Clamp();
        return Current;
    }

    public static void Save(AppSettings settings)
    {
        settings.Clamp();
        Directory.CreateDirectory(SecretsStore.AppDataDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        Current = settings.Clone();
        Changed?.Invoke();
    }
}
