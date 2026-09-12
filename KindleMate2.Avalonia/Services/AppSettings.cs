using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 应用级设置(主题 / 语言 / 上次打开的库)。
///
/// 为什么不放数据库里:主题与"上次打开的库"必须在打开库之前就可用,
/// 而桌面版的 SettingRepository 依赖 appsettings 固定在 KM2.dat 上;因此这里
/// 用一份独立的小 JSON,放在各平台标准的应用数据目录,天然跨平台。
/// </summary>
public sealed class AppSettings {
    [JsonPropertyName("theme")] public string Theme { get; set; } = "system";
    [JsonPropertyName("language")] public string Language { get; set; } = "auto";
    [JsonPropertyName("lastDatabase")] public string LastDatabase { get; set; } = string.Empty;

    [JsonIgnore] public string FilePath { get; private set; } = string.Empty;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultDirectory {
        get {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(root)) {
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }
            return Path.Combine(root, "KindleMate2");
        }
    }

    public static AppSettings Load(string? directory = null) {
        var dir = directory ?? DefaultDirectory;
        var path = Path.Combine(dir, "settings.json");
        try {
            if (File.Exists(path)) {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
                loaded.FilePath = path;
                return loaded;
            }
        } catch (Exception ex) {
            Console.WriteLine($"[AppSettings.Load] {ex.Message}");
        }
        return new AppSettings { FilePath = path };
    }

    public void Save() {
        try {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        } catch (Exception ex) {
            Console.WriteLine($"[AppSettings.Save] {ex.Message}");
        }
    }

    /// <summary>把语言设置应用到当前线程文化(影响日期/数字格式与资源查找)。</summary>
    public void ApplyCulture() {
        var culture = Resolve(Language);
        if (culture == null) return;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static CultureInfo? Resolve(string language) => language switch {
        "zh-hans" => new CultureInfo("zh-Hans"),
        "zh-hant" => new CultureInfo("zh-Hant"),
        "en" => new CultureInfo("en"),
        _ => null
    };
}
