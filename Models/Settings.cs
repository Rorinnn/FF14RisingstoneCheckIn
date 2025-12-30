using System.Text.Json;
using System.Text.Json.Serialization;

namespace FF14RisingstoneCheckIn.Models;

public class Settings
{
    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory,
        "settings.json");

    public string Account { get; set; } = string.Empty;
    public string SavedRisingstoneCookie { get; set; } = string.Empty;
    public string SavedUserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36 Edg/137.0.0.0";
    public bool EnableAutoSignIn { get; set; } = true;
    public bool EnableAutoStart { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public DateTime? LastSignInTime { get; set; }
    public DateTime? CookieSavedTime { get; set; }

    [JsonIgnore]
    public string BaseUrl => "https://apiff14risingstones.web.sdo.com";

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
