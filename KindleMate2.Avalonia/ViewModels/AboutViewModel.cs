using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using KindleMate2.Avalonia.Services;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.ViewModels;

/// <summary>关于页 VM。桌面版显示固定 KM2.dat 的大小,这里改为显示"当前打开的库"。</summary>
public sealed class AboutViewModel {
    public string Product { get; private init; } = "Kindle Mate 2";
    public string Version { get; private init; } = string.Empty;
    public string Copyright { get; private init; } = string.Empty;
    public string ProgramPath { get; private init; } = string.Empty;
    public string DatabaseName { get; private init; } = string.Empty;
    public string DatabaseSize { get; private init; } = string.Empty;
    public string DatabasePath { get; private init; } = string.Empty;
    public string RepoUrl { get; private init; } = AppConstants.RepoUrl;
    public string Runtime { get; private init; } = string.Empty;

    public static AboutViewModel Load(DatabaseSession? session) {
        var assembly = typeof(AboutViewModel).Assembly;
        var product = GetAttribute<AssemblyProductAttribute>(assembly)?.Product ?? "Kindle Mate 2";
        var copyright = GetAttribute<AssemblyCopyrightAttribute>(assembly)?.Copyright ?? string.Empty;
        var version = assembly.GetName().Version?.ToString() ?? string.Empty;

        var dbPath = session?.DatabasePath ?? string.Empty;
        var dbName = dbPath.Length > 0 ? Path.GetFileName(dbPath) : "未打开数据库";
        var dbSize = string.Empty;
        if (dbPath.Length > 0 && File.Exists(dbPath)) {
            dbSize = FormatSize(new FileInfo(dbPath).Length);
        }

        return new AboutViewModel {
            Product = product,
            Version = version,
            Copyright = copyright,
            ProgramPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            DatabaseName = dbName,
            DatabaseSize = dbSize,
            DatabasePath = dbPath,
            Runtime = $"{Environment.OSVersion.Platform} · .NET {Environment.Version}"
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
