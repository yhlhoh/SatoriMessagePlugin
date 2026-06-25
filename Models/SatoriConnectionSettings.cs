using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SatoriMessagePlugin.Models;

public partial class SatoriConnectionSettings : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    [ObservableProperty]
    private string _satoriWsUrl = "";

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _autoReconnect = true;

    [ObservableProperty]
    private double _reconnectDelaySeconds = 5.0;

    [ObservableProperty]
    private string _mutedGroupsText = "";

    [ObservableProperty]
    private string _mutedSendersText = "";

    [JsonIgnore]
    public IReadOnlyList<string> MutedGroups => SplitLines(MutedGroupsText);

    [JsonIgnore]
    public IReadOnlyList<string> MutedSenders => SplitLines(MutedSendersText);

    [JsonIgnore]
    public string SettingsDirectory { get; set; } = "";

    [JsonIgnore]
    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public static SatoriConnectionSettings Load(string directory)
    {
        var path = Path.Combine(directory, "settings.json");
        try
        {
            if (!File.Exists(path))
            {
                return new SatoriConnectionSettings { SettingsDirectory = directory };
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<SatoriConnectionSettings>(json) ?? new SatoriConnectionSettings();
            settings.SettingsDirectory = directory;
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SatoriMessagePlugin] 加载设置失败，使用默认设置: {ex.Message}");
            return new SatoriConnectionSettings { SettingsDirectory = directory };
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public void Normalize()
    {
        ReconnectDelaySeconds = Math.Clamp(ReconnectDelaySeconds, 1.0, 60.0);
        SatoriWsUrl = (SatoriWsUrl ?? "").Trim();
    }

    private static IReadOnlyList<string> SplitLines(string value)
    {
        return (value ?? "")
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }
}
