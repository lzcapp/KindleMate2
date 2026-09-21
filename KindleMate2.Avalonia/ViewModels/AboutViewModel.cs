using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using KindleMate2.Avalonia.Services;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>关于页 VM。桌面版显示固定 KM2.dat 的大小,这里改为显示"当前打开的库"。</summary>
public sealed class AboutViewModel {
    public string Product { get; private init; } = "Kindle Mate 2";
    public string Version { get; private init; } = string.Empty;
    public string Copyright { get; private init; } = string.Empty;
    public string ProgramPath { get; private init; } = string.Empty;

    /// <summary>
    /// 数据目录 —— 当前库所在目录(无库时为 <see cref="AppPaths.DataDirectory"/>,
    /// 与原版"程序目录即数据目录"的约定一致),KM2.dat、备份、导入导出都落在这里。
    /// 与 ProgramPath 一样保证非空,这样即便还没打开库,「数据路径」这一行也仍可点击跳转。
    /// </summary>
    public string DataPath { get; private init; } = string.Empty;
    public string DatabaseName { get; private init; } = string.Empty;
    public string DatabaseSize { get; private init; } = string.Empty;
    public string RepoUrl { get; private init; } = AppConstants.RepoUrl;
    public string Runtime { get; private init; } = string.Empty;

    /// <summary>「版本 2026.9.13.0」——文案走资源,便于多语言。</summary>
    public string VersionLabel => string.Format(CultureInfo.CurrentCulture, Strings.Ui_About_Version, Version);

    /// <summary>数据库体积,形如「(7.02 MB)」;无体积时为空。</summary>
    public string DatabaseSizeLabel => DatabaseSize.Length > 0 ? $"({DatabaseSize})" : string.Empty;

    public static AboutViewModel Load(DatabaseSession? session) {
        var assembly = typeof(AboutViewModel).Assembly;
        var product = GetAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Kindle Mate 2";
        var copyright = GetAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright ?? string.Empty;
        var version = assembly.GetName().Version?.ToString() ?? string.Empty;

        var dbPath = session?.DatabasePath ?? string.Empty;
        var dbName = dbPath.Length > 0 ? Path.GetFileName(dbPath) : Strings.Ui_About_NoDatabase;
        var dbSize = string.Empty;
        if (dbPath.Length > 0 && File.Exists(dbPath)) {
            dbSize = FormatSize(new FileInfo(dbPath).Length);
        }

        // 「数据路径」= 当前库所在目录;还没打开库时退回约定的数据目录,
        // 这样这一行不会变成空白、依然可点击跳转。
        var dataPath = session?.WorkDirectory is { Length: > 0 } workDirectory
            ? workDirectory
            : AppPaths.DataDirectory;

        return new AboutViewModel {
            Product = product,
            Version = version,
            Copyright = copyright,
            // 目录路径去掉结尾分隔符只为显示好看;TrimEndingDirectorySeparator 会保留根目录("/")不把它清空。
            ProgramPath = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            DataPath = Path.TrimEndingDirectorySeparator(dataPath),
            DatabaseName = dbName,
            DatabaseSize = dbSize,
            Runtime = string.Format(CultureInfo.CurrentCulture, Strings.Ui_About_RuntimeFormat,
                Environment.OSVersion.Platform, Environment.Version)
        };
    }

    private static T? GetAttribute<T>(Assembly assembly) where T : Attribute =>
        assembly.GetCustomAttributes(typeof(T), false) is { Length: > 0 } attributes
            ? (T)attributes[0]
            : null;

    private static string FormatSize(long bytes) {
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) {
            value /= 1024;
            unit++;
        }
        return string.Concat(value.ToString("0.##", CultureInfo.CurrentCulture), " ", units[unit]);
    }
}
