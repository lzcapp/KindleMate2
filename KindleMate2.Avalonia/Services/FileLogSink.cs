using System;
using System.IO;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 把 <see cref="AppLog"/> 的出口接到数据目录下的 <c>error.log</c>。
///
/// 这是 UI 层对库层日志抽象的**唯一实现** —— 库层只认识"一段文本",不认识文件、
/// 也不该知道 UI 的存在(这正是把 AppLog 放到 Shared 的原因)。
///
/// 日志为什么放数据目录:与原版"程序目录即数据目录"的模型一致(库、备份、导入、导出都在这里),
/// 便于让用户直接找到;`error.log` 已被 .gitignore 的 `*.log` 规则覆盖,不会污染仓库。
/// </summary>
public static class FileLogSink {
    /// <summary>日志文件名。</summary>
    public const string FileName = "error.log";

    private static bool _initialized;

    /// <summary>实际生效的日志路径,启动时确定一次(供诊断:日志究竟落到了哪)。</summary>
    public static string LogPath { get; private set; } = string.Empty;

    /// <summary>启动时调用一次,把日志落到数据目录。</summary>
    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        LogPath = ResolveWritablePath();
        AppLog.SetSink(message => {
            try {
                File.AppendAllText(
                    LogPath,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
            } catch {
                // 写不下去只能作罢 —— 不能让日志失败影响主流程
            }
        });
    }

    /// <summary>
    /// 在候选目录里挑第一个**能写**的放日志:优先数据目录(便于用户找到),不可写时退回
    /// 平台的应用数据目录。
    ///
    /// 不这么做的后果是日志**静默丢失** —— 而"没有日志"恰好是最难排查的那类故障:只要进程的
    /// 当前目录不可写(例如直接以 <c>dotnet KindleMate2.dll</c> 启动、或从只读目录启动),
    /// 写入就会每次抛异常并被 catch 掉,用户与开发者都看不到任何痕迹。
    /// </summary>
    private static string ResolveWritablePath() {
        foreach (var directory in new[] { AppPaths.DataDirectory, AppSettings.DefaultDirectory }) {
            if (IsWritable(directory)) {
                return Path.Combine(directory, FileName);
            }
        }

        // 两处都不可写:仍返回数据目录下的路径(写入时照样被吞掉),至少路径本身可预期。
        return Path.Combine(AppPaths.DataDirectory, FileName);
    }

    /// <summary>用"真写一个探针文件"判断可写 —— 只看目录是否存在会漏掉权限与只读挂载。</summary>
    private static bool IsWritable(string directory) {
        try {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, "." + FileName + ".probe");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        } catch {
            return false;
        }
    }
}
