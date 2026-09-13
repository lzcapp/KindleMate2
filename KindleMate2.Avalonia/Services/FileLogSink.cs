using System;
using System.IO;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 把 <see cref="AppLog"/> 的出口接到程序目录下的 <c>error.log</c>。
///
/// 这是 UI 层对库层日志抽象的**唯一实现** —— 库层只认识"一段文本",不认识文件、
/// 也不该知道 UI 的存在(这正是把 AppLog 放到 Shared 的原因)。
///
/// 日志为什么放程序目录:与原版"程序目录即数据目录"的模型一致(库、备份、导入、导出都在这里),
/// 便于让用户直接找到;`error.log` 已被 .gitignore 的 `*.log` 规则覆盖,不会污染仓库。
/// </summary>
public static class FileLogSink {
    /// <summary>日志文件名。</summary>
    public const string FileName = "error.log";

    private static bool _initialized;

    /// <summary>启动时调用一次,把日志落到程序目录。</summary>
    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        AppLog.SetSink(message => {
            try {
                File.AppendAllText(
                    Path.Combine(Environment.CurrentDirectory, FileName),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
            } catch {
                // 写不下去只能作罢 —— 不能让日志失败影响主流程
            }
        });
    }
}
