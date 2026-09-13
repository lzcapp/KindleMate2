namespace KindleMate2.Shared.Diagnostics;

/// <summary>
/// 全局日志出口 —— **库层（Application / Infrastructure / Devices）只依赖这里**，
/// 从而不必引用 UI 层，也不会像 <c>Console.WriteLine</c> 那样在 WinExe 下写进虚空
/// （WinExe 没有控制台，写出去的消息无人接收）。
///
/// 用法：App 层在启动时调用一次 <see cref="SetSink"/> 注入真正的写入实现
/// （见 KindleMate2.Avalonia 的 <c>FileLogSink</c>）。各层任何位置都可以直接
/// <see cref="Write(string)"/> / <see cref="Write(Exception)"/>。
///
/// 约定：
/// <list type="bullet">
///   <item>未注入 sink 时落到 <see cref="Console.Error"/>（控制台 / CI 场景仍有输出）。</item>
///   <item><b>绝不抛异常</b> —— 日志自身失败再抛，会把"可提示的错误"变成"静默崩溃"。</item>
///   <item>线程安全：导入等操作在后台线程上报，写入需加锁。</item>
/// </list>
/// </summary>
public static class AppLog {
    private static readonly object Gate = new();
    private static Action<string>? _sink;

    /// <summary>注入写入实现（App 层启动时调用一次）。传 null 可还原为默认输出。</summary>
    public static void SetSink(Action<string>? sink) {
        lock (Gate) {
            _sink = sink;
        }
    }

    /// <summary>记录一段文本。</summary>
    public static void Write(string? message) {
        if (string.IsNullOrWhiteSpace(message)) return;
        try {
            // 在锁内调用 sink:日志写入罕见,串行化可避免多线程(如后台导入)交错写同一文件。
            // 锁可重入,故 sink 内部若再调 AppLog 也不会自锁。
            lock (Gate) {
                if (_sink != null) {
                    _sink(message);
                } else {
                    Console.Error.WriteLine(message);
                }
            }
        } catch {
            // 日志失败只能作罢，绝不能因此再抛
        }
    }

    /// <summary>记录一个异常（含堆栈）。</summary>
    public static void Write(Exception? exception) {
        if (exception == null) return;
        Write(exception.ToString());
    }
}
