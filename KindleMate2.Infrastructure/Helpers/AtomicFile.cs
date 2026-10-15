using System;
using System.IO;

namespace KindleMate2.Infrastructure.Helpers;

/// <summary>
/// 原子写文本文件:先写唯一的临时文件,再 <see cref="File.Move(string, string, bool)"/> 原子替换目标。
///
/// 为什么需要:<c>File.WriteAllText</c> 直接覆写目标,进程在写到一半时崩溃/被杀、或两个调用并发写,
/// 都会留下**半截文件**。对配置文件这类"启动时读、读不出来就静默回退默认值"的用途,损坏意味着
/// 用户的主题 / 语言 / 跳过版本等选择被无声丢掉 —— 现象是"设置莫名其妙没了",极难排查。
/// 临时文件用 GUID 命名,避免并发写时两个调用互踩同一个临时名。
///
/// 失败时清理临时文件并向上抛:是否容错由调用方决定(设置保存是尽力而为,调用方自行吞并记日志)。
/// </summary>
public static class AtomicFile {
    public static void WriteAllText(string path, string contents) {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(tempPath, contents);
            File.Move(tempPath, path, overwrite: true);
        } catch {
            // 写/替换失败时清掉临时文件,别在目标目录里留垃圾(清理自身失败也不掩盖原始异常)。
            try {
                if (File.Exists(tempPath)) {
                    File.Delete(tempPath);
                }
            } catch {
                // best effort
            }
            throw;
        }
    }
}
