namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 极简文件日志,用于"不能弹窗、也不能抛"的场合(未捕获异常、退出阶段的清理失败等)。
///
/// 为什么不用 <c>Console.WriteLine</c>:Windows 上本程序是 <c>WinExe</c>(见 csproj 的
/// <c>OutputType</c> 条件),**没有控制台**,写到 Console 的消息会直接消失。
///
/// 为什么放在程序目录:<c>error.log</c> 与原版"程序目录即数据目录"的模型一致
/// (库、备份、导入、导出都在这里),便于让用户直接找到;该文件名已被 .gitignore
/// 的 <c>*.log</c> 规则覆盖,不会污染仓库。
///
/// **本类绝不抛异常** —— 日志自身失败再抛,会把"可提示的错误"变成"静默崩溃"。
/// </summary>
public static class AppLog {
    public const string FileName = "error.log";

    public static void Write(Exception ex) {
        try {
            File.AppendAllText(
                Path.Combine(Environment.CurrentDirectory, FileName),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        } catch {
            // 日志写不下去只能作罢,不能因此再抛
        }
    }
}
