using System.IO;
using System.Text.Json;

namespace AchievementCreationTool;

/// <summary>What the tool remembers between runs, in %AppData%\PotPAchievementTool\settings.json</summary>
public sealed class AppSettings
{
    public string? LastPack { get; set; }
    public string? GameFolder { get; set; }
    public bool MakeZip { get; set; } = true;

    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PotPAchievementTool", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The game's folder: the one chosen before, else Steam's or GOG's usual places</summary>
    public static string? FindGame()
    {
        if (Current.GameFolder is { } chosen && Directory.Exists(Path.Combine(chosen, "bin")))
            return chosen;
        var guesses = new List<string>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            guesses.Add(Path.Combine(drive.Name, "Program Files (x86)", "Steam", "steamapps", "common", "The Witcher 3"));
            guesses.Add(Path.Combine(drive.Name, "Steam", "steamapps", "common", "The Witcher 3"));
            guesses.Add(Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "The Witcher 3"));
            guesses.Add(Path.Combine(drive.Name, "GOG Games", "The Witcher 3 Wild Hunt GOTY"));
            guesses.Add(Path.Combine(drive.Name, "Program Files (x86)", "GOG Galaxy", "Games", "The Witcher 3 Wild Hunt GOTY"));
        }
        return guesses.FirstOrDefault(g => Directory.Exists(Path.Combine(g, "bin")));
    }
}
