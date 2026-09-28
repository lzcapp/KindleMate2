using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using KindleMate2.Application.Models;
using KindleMate2.Avalonia.Services;
using KindleMate2.Shared;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Diagnostics;
using KindleMate2.Shared.Entities;

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

    // ———— 「设备」段:连着 Kindle 时才出现,未连接整段(含分隔线)不显示。 ————

    /// <summary>设备是否在线并拿到了概览 —— 段级可见性开关。</summary>
    public bool IsDevicePresent { get; private init; }

    /// <summary>连接方式(USB 文件卷 / MTP);来自资源,非 null。</summary>
    public string? DeviceConnection { get; private init; }

    /// <summary>卷路径(仅 USB 机型);拿不到为 null,行隐藏。</summary>
    public string? DevicePath { get; private init; }

    /// <summary>固件(version.txt 第一行非空行);拿不到为 null,行隐藏。</summary>
    public string? DeviceFirmware { get; private init; }

    /// <summary>存储(「可用 x / 共 y」);拿不到为 null,行隐藏。</summary>
    public string? DeviceStorage { get; private init; }

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

        // 「设备」段:未连接 / 没打开库 → 整段隐藏;任何异常也只隐藏不打断关于窗口。
        var device = GetDeviceSummary(session);

        return new AboutViewModel {
            Product = product,
            Version = version,
            Copyright = copyright,
            // 目录路径去掉结尾分隔符只为显示好看;TrimEndingDirectorySeparator 会保留根目录("/")不把它清空。
            ProgramPath = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),
            DataPath = Path.TrimEndingDirectorySeparator(dataPath),
            DatabaseName = dbName,
            DatabaseSize = dbSize,
            IsDevicePresent = device != null,
            DeviceConnection = device switch {
                { Type: Device.Type.USB } => Strings.Ui_About_DeviceConnectionUsb,
                { Type: Device.Type.MTP } => Strings.Ui_About_DeviceConnectionMtp,
                _ => null
            },
            DevicePath = device?.DrivePath,
            DeviceFirmware = device?.Firmware,
            DeviceStorage = FormatStorage(device),
            // 平台那一半原来用 Environment.OSVersion.Platform —— 它在 macOS 与 Linux 上**都**返回
            // "Unix"(本机实测),于是这一行**分不出**用户装的是哪个平台的包;Windows 上则是原始枚举名
            // "Win32NT"。而这一行的用途恰恰是排查平台问题(例如"macOS 包里有没有 Devices.MacOS.dll"),
            // 所以换成 RuntimeInformation:
            //   OSDescription        → macOS 27.0.0 / Ubuntu 24.04 / Microsoft Windows 10.0.22631
            //   RuntimeIdentifier    → osx-arm64 / linux-x64 / win-x64(**与发布资产命名同一口径**)
            //   FrameworkDescription → ".NET 10.0.12"(自带 ".NET ",格式串不必再写死)
            Runtime = string.Format(CultureInfo.CurrentCulture, Strings.Ui_About_RuntimeFormat,
                RuntimeInformation.OSDescription,
                RuntimeInformation.RuntimeIdentifier,
                RuntimeInformation.FrameworkDescription)
        };
    }

    /// <summary>
    /// 取设备概览:没打开库 → null;实现侧承诺"未连接返回 null、取不到的字段留空、不额外开会话",
    /// 这里再兜一层异常 —— 关于窗口只是展示,任何读取失败都以"整段隐藏"收场,绝不打断。
    /// </summary>
    private static DeviceSummary? GetDeviceSummary(DatabaseSession? session) {
        try {
            return session?.DeviceManager?.GetDeviceInfo();
        } catch (Exception ex) {
            AppLog.Write($"[About] 读取设备信息失败:{ex}");
            return null;
        }
    }

    /// <summary>「存储」行文案:总容量/可用量都拿得到才显示,否则 null(行隐藏)。</summary>
    private static string? FormatStorage(DeviceSummary? device) =>
        device is { TotalBytes: { } total, FreeBytes: { } free }
            ? string.Format(CultureInfo.CurrentCulture, Strings.Ui_About_DeviceStorageFormat,
                FormatSize(free), FormatSize(total))
            : null;

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
