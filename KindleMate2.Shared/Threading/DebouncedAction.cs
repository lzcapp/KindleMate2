using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Shared.Threading;

/// <summary>
/// 「多次触发 → 静默 <paramref name="delay"/> 后执行一次回调」的防抖器,并把**释放竞态**收在一处。
///
/// 为什么需要它:设备监听的取消/释放路径**不等待**在飞的轮询或 WMI 回调 —— 那些回调可能在
/// <see cref="Dispose"/> 之后才走到"安排一次复检"这一步。天真的实现会在这时**重新建出**
/// 刚被释放的计时器,于是释放之后又补报一次设备状态(macOS 轮询实现的
/// <c>Dispose_StopsWatching</c> 在慢 CI 上就因此偶发失败)。POSIX 与 Windows 两套设备实现
/// 都要这段逻辑,故抽出来共用,避免修一处漏一处、也避免两边各自演化出不同的释放语义。
///
/// 语义:
/// <list type="bullet">
///   <item><see cref="Schedule"/> 反复调用只认最后一次(重新计时),回调至多执行一次;</item>
///   <item><see cref="Dispose"/> 之后 <see cref="Schedule"/> 为 no-op,已到期的回调也会被丢弃;</item>
///   <item>回调异常在此捕获并记日志 —— 计时器回调里逃逸的异常会终结进程,不能放任。</item>
/// </list>
/// </summary>
public sealed class DebouncedAction : IDisposable {
    /// <summary>守 <see cref="_timer"/> 的创建/释放。同时保证 Schedule 与 Dispose 之间的可见性。</summary>
    private readonly object _lockObj = new object();

    private readonly TimeSpan _delay;
    private readonly Action _callback;

    private System.Threading.Timer? _timer;

    /// <summary>已释放标志。回调线程不持锁,故用 volatile 做一次快速判断。</summary>
    private volatile bool _disposed;

    /// <param name="delay">静默多久才执行回调;必须为正。</param>
    /// <param name="callback">到期执行的动作。异常由本类捕获记录,不外抛。</param>
    public DebouncedAction(TimeSpan delay, Action callback) {
        if (delay <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "防抖时长必须为正。");
        }
        _delay = delay;
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>安排/重置一次防抖。释放后调用无任何效果(不会新建计时器)。</summary>
    public void Schedule() {
        lock (_lockObj) {
            if (_disposed) {
                return;
            }
            if (_timer == null) {
                _timer = new System.Threading.Timer(static state => ((DebouncedAction)state!).Fire(),
                    this, _delay, Timeout.InfiniteTimeSpan);
            } else {
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void Fire() {
        // Dispose 之后到达的回调直接丢弃:订阅方此时多半已经在拆界面了。
        if (_disposed) {
            return;
        }
        try {
            _callback();
        } catch (Exception ex) {
            AppLog.Write($"[DebouncedAction] 回调异常:{ex}");
        }
    }

    /// <summary>停止并释放。幂等;此后不再有任何回调。</summary>
    public void Dispose() {
        lock (_lockObj) {
            if (_disposed) {
                return;
            }
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
