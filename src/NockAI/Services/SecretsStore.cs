using System.IO;
using System.Text.Json;

namespace NockAI.Services;

public sealed class AppSecrets
{
    public string GroqApiKey { get; set; } = "";
    public string GroqModel { get; set; } = "qwen/qwen3.8-27b";
}

public static class SecretsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NockAI");

    public static string AppDataSecretsPath => Path.Combine(AppDataDir, "Secrets.local.json");

    public static string LocalSecretsPath =>
        Path.Combine(AppContext.BaseDirectory, "Secrets.local.json");

    public static AppSecrets Load()
    {
        foreach (var path in CandidatePaths())
        {
            if (!File.Exists(path))
                continue;

            var secrets = ReadFile(path);
            if (string.IsNullOrWhiteSpace(secrets.GroqApiKey) ||
                secrets.GroqApiKey.Contains("your_key", StringComparison.OrdinalIgnoreCase))
                continue;

            // Copy legacy QuickCalcAI key into NockAI AppData
            if (!string.Equals(path, AppDataSecretsPath, StringComparison.OrdinalIgnoreCase) &&
                path.Contains("QuickCalcAI", StringComparison.OrdinalIgnoreCase) &&
                !File.Exists(AppDataSecretsPath))
            {
                try { Save(secrets, preferAppData: true); } catch { /* ignore */ }
            }

            return secrets;
        }

        throw new FileNotFoundException(
            "No Groq API key configured. Add Secrets.local.json or complete first-run setup.");
    }

    public static bool TryLoad(out AppSecrets secrets)
    {
        try
        {
            secrets = Load();
            return true;
        }
        catch
        {
            secrets = new AppSecrets();
            return false;
        }
    }

    public static void Save(AppSecrets secrets, bool preferAppData = true)
    {
        if (string.IsNullOrWhiteSpace(secrets.GroqApiKey))
            throw new InvalidOperationException("GroqApiKey is empty.");

        if (string.IsNullOrWhiteSpace(secrets.GroqModel))
            secrets.GroqModel = "qwen/qwen3.8-27b";

        var path = preferAppData ? AppDataSecretsPath : LocalSecretsPath;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(secrets, JsonOptions));
    }

    private static IEnumerable<string> CandidatePaths()
    {
        yield return LocalSecretsPath;
        yield return AppDataSecretsPath;

        // One-time migrate from old QuickCalcAI install
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickCalcAI",
            "Secrets.local.json");
    }

    private static AppSecrets ReadFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSecrets>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Invalid secrets file: {path}");
    }
}
