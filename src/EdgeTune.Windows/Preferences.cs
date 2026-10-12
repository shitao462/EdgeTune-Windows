using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
namespace EdgeTune;
public enum Player { NetEase, QQMusic, Spotify, AppleMusic }
public enum WindowTheme { Dark, Light, Glass }
public sealed class Preferences
{
    public Player Player { get; set; } = Player.NetEase;
    public WindowTheme WindowTheme { get; set; } = WindowTheme.Dark;
    public int Color { get; set; }
    public bool Pulse { get; set; } = true;
    public bool Pinned { get; set; }
    public int HotkeyModifiers { get; set; } = 3; // Ctrl + Alt
    public int HotkeyKey { get; set; } = 0x4D;
    public double VerticalRatio { get; set; } = 0.45;
    public static Preferences Load()
    {
        try { var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(Log.Folder, "settings.json"))) ?? new();
            if (!Enum.IsDefined(value.Player)) value.Player = Player.NetEase;
            if (!Enum.IsDefined(value.WindowTheme)) value.WindowTheme = WindowTheme.Dark;
            value.Color = Math.Clamp(value.Color, 0, 7); value.VerticalRatio = Math.Clamp(value.VerticalRatio, 0, 1); return value;
        } catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Log.Folder);
        string path = Path.Combine(Log.Folder, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this)); File.Move(path + ".tmp", path, true);
    }
    public static string Name(Player player) => player switch { Player.NetEase => "网易云音乐", Player.QQMusic => "QQ 音乐", Player.Spotify => "Spotify", _ => "Apple Music" };
    public static bool Matches(Player player, string appId) => player switch
    {
        Player.NetEase => Contains(appId, "cloudmusic", "netease", "163music"),
        Player.QQMusic => Contains(appId, "qqmusic", "qq.music", "tencent.qq"),
        Player.Spotify => Contains(appId, "spotify"),
        _ => Contains(appId, "applemusic", "appleinc.applemusic", "itunes")
    };
    private static bool Contains(string text, params string[] tokens) { foreach (var token in tokens) if (text.Contains(token, StringComparison.OrdinalIgnoreCase)) return true; return false; }
    public static bool AutoStart
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("EdgeTune") != null; }
        set { using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) key.SetValue("EdgeTune", $"\"{Environment.ProcessPath}\""); else key.DeleteValue("EdgeTune", false); }
    }
}
