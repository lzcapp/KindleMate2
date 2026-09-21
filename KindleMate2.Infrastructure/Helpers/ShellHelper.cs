using System.Diagnostics;

namespace KindleMate2.Infrastructure.Helpers;

/// <summary>
/// 在系统文件管理器中打开目录 / 定位并选中文件。
///
/// 背景:原版把 Windows 专用的 <c>explorer.exe</c> 与 <c>/select,</c> 写死在
/// <c>AppConstants</c> 里、由调用方直接 <c>Process.Start</c> —— macOS / Linux 上并不存在这个
/// 可执行文件,于是「统计页截图后打开所在位置」在两个平台上必然抛异常而功能失效。
/// 平台差异一律收进本类,调用方不再感知,<c>AppConstants</c> 也不再提供 Windows 专有命令名。
/// </summary>
public static class ShellHelper {
    /// <summary>Windows 资源管理器:命令名与「选中文件」参数(沿用原版写法,Windows 行为不变)。</summary>
    private const string ExplorerFileName = "explorer.exe";
    private const string ExplorerSelectArgument = "/select,";

    /// <summary>macOS 的 open(1):<c>-R</c> 即「在 Finder 中显示并选中」,与 Windows 的 /select 语义等价。</summary>
    private const string MacOpenFileName = "open";
    private const string MacRevealArgument = "-R";

    /// <summary>
    /// 打开目录(等价于在文件管理器中双击该文件夹)。
    /// 目录是否需要预先创建由调用方决定,这里不做隐式创建 —— 有的调用点(如「关于」页打开程序路径)
    /// 刻意只打开、不产生副作用。
    /// </summary>
    public static void OpenDirectory(string directoryPath) {
        if (string.IsNullOrWhiteSpace(directoryPath)) {
            return;
        }

        // 用「路径本身 + UseShellExecute」是 .NET 的跨平台做法:三端各自交给系统处理
        // (Windows 走 ShellExecute、macOS 走 open、Linux 走 xdg-open),不必自己拼命令行。
        Process.Start(new ProcessStartInfo { FileName = directoryPath, UseShellExecute = true });
    }

    /// <summary>
    /// 在文件管理器中定位并选中该文件。没有「选中文件」能力的平台退化为打开所在目录,不抛异常。
    /// 异常由调用方按各自语境处理(有的写入日志,有的静默),这里不吞。
    /// </summary>
    public static void RevealFile(string filePath) {
        if (string.IsNullOrWhiteSpace(filePath)) {
            return;
        }

        Process.Start(BuildRevealStartInfo(filePath, OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()));
    }

    /// <summary>
    /// 构造「定位文件」的启动参数。平台由参数显式给出、而非就地调用 <c>OperatingSystem</c>,
    /// 以便单测把三种平台逐一覆盖 —— 这里正是原缺陷所在(原先完全没有平台分支),值得钉死。
    /// </summary>
    public static ProcessStartInfo BuildRevealStartInfo(string filePath, bool isWindows, bool isMacOs) {
        if (isWindows) {
            return new ProcessStartInfo {
                FileName = ExplorerFileName,
                // 保持原版的「/select, + 带引号路径」单串写法:explorer.exe 对空格分隔的
                // 等价写法支持并不一致,这里不拿能用的平台去冒险。
                Arguments = ExplorerSelectArgument + "\"" + filePath + "\"",
                UseShellExecute = true
            };
        }

        if (isMacOs) {
            var startInfo = new ProcessStartInfo {
                FileName = MacOpenFileName,
                // open 是 /usr/bin 下的普通可执行文件,无需 shell 参与;用 ArgumentList 而非拼字符串,
                // 由运行时负责转义,路径含空格 / 引号 / 中文都不会出错。
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(MacRevealArgument);
            startInfo.ArgumentList.Add(filePath);
            return startInfo;
        }

        // Linux 等的 xdg-open 没有「选中文件」的等价能力:退化为打开所在目录。
        // 只有文件名(相对路径)时退回当前目录,不能给出空 FileName。
        var directory = Path.GetDirectoryName(filePath);
        return new ProcessStartInfo {
            FileName = string.IsNullOrEmpty(directory) ? "." : directory,
            UseShellExecute = true
        };
    }
}
