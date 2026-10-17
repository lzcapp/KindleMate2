using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using KindleMate2.Infrastructure.Helpers;
using KindleMate2.Shared.Diagnostics;

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

    /// <summary>
    /// 用户点过「跳过此版本」的那个版本号(空 = 没跳过)。
    /// 只抑制**启动**自动弹窗;手动「检查更新」照常提示(那是用户的明确动作)。
    /// </summary>
    [JsonPropertyName("skippedUpdateVersion")] public string SkippedUpdateVersion { get; set; } = string.Empty;

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
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, Options);
                if (loaded == null) {
                    // 合法 JSON 但内容是 null(如文件里只有一个 "null")——反序列化不抛异常,
                    // 却会静默回退默认值,把主题/语言/跳过版本全丢掉。至少留一条日志。
                    AppLog.Write("[AppSettings] settings.json 反序列化结果为空,回退默认设置");
                    return new AppSettings { FilePath = path };
                }
                loaded.FilePath = path;
                return loaded;
            }
        } catch (Exception ex) {
            AppLog.Write(ex);
        }
        return new AppSettings { FilePath = path };
    }

    public void Save() {
        try {
            // 原子写:写临时文件 + 原子替换 —— 直接 File.WriteAllText 在崩溃/并发下会留下半截
            // JSON,Load 只得静默回退默认值,用户的主题/语言/跳过版本就这样无声丢掉了。
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        } catch (Exception ex) {
            AppLog.Write(ex);
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
