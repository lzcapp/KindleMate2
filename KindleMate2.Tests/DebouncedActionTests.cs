using Xunit;
using KindleMate2.Shared.Threading;

namespace KindleMate2.Tests;

/// <summary>
/// 设备监听共用的防抖器。重点钉住**释放竞态**:设备监听的取消/释放不等待在飞的轮询或 WMI 回调,
/// 那些回调可能在 Dispose 之后才来调度复检 —— 天真的实现会重建计时器、在释放后补报一次。
/// (macOS 的 <c>Dispose_StopsWatching</c> 在慢 CI 上偶发失败正是这个成因。)
/// </summary>
public sealed class DebouncedActionTests {
    [Fact]
    public async Task Schedule_FiresOnceAfterTheQuietPeriod() {
        var fired = 0;
        using var action = new DebouncedAction(TimeSpan.FromMilliseconds(60), () => Interlocked.Increment(ref fired));

        action.Schedule();
        Assert.Equal(0, Volatile.Read(ref fired));   // 静默期内不触发

        await WaitUntil(() => Volatile.Read(ref fired) == 1, TimeSpan.FromSeconds(2));
        await Task.Delay(120);
        Assert.Equal(1, Volatile.Read(ref fired));   // 且只触发一次
    }

    [Fact]
    public async Task RepeatedSchedules_CoalesceIntoASingleCallback() {
        var fired = 0;
        using var action = new DebouncedAction(TimeSpan.FromMilliseconds(80), () => Interlocked.Increment(ref fired));

        // 连续重置:几次调用远快于防抖窗口,共用一个计时器,只应触发一次。
        // 刻意不用"间隔 N ms 再 Schedule"来造时序 —— 那在负载高的 CI 上会因 Task.Delay 超时抖动
        // (macOS runner 上实测过一次:预期 1、实际 2)。
        action.Schedule();
        action.Schedule();
        action.Schedule();

        await WaitUntil(() => Volatile.Read(ref fired) == 1, TimeSpan.FromSeconds(2));
        await Task.Delay(120);
        Assert.Equal(1, Volatile.Read(ref fired));
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndStopsAPendingCallback() {
        var fired = 0;
        // 防抖时长取得足够长,确保「安排 → 释放」之间不会因为调度延迟就先触发。
        var action = new DebouncedAction(TimeSpan.FromMilliseconds(500), () => Interlocked.Increment(ref fired));

        action.Schedule();
        action.Dispose();
        action.Dispose();   // 二次释放必须安全

        await Task.Delay(700);   // 越过原定到期时刻:被释放的计时器不该再触发
        Assert.Equal(0, Volatile.Read(ref fired));
    }

    /// <summary>
    /// 回归:释放之后才到达的调度(取消轮询不等待的那一轮 / 并发 WMI 事件)不得再建计时器、
    /// 也不得补报。
    /// </summary>
    [Fact]
    public async Task Schedule_AfterDispose_DoesNothing() {
        var fired = 0;
        var action = new DebouncedAction(TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref fired));

        action.Dispose();
        action.Schedule();   // 释放后晚到的调度
        action.Schedule();

        await Task.Delay(200);
        Assert.Equal(0, Volatile.Read(ref fired));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveDelayAndNullCallback() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DebouncedAction(TimeSpan.Zero, () => { }));
        Assert.Throws<ArgumentNullException>(() => new DebouncedAction(TimeSpan.FromMilliseconds(1), null!));
    }

    /// <summary>
    /// 回调异常必须被吞掉:计时器回调里逃逸的异常会终结整个进程。能走到第二次调度即通过。
    /// </summary>
    [Fact]
    public async Task CallbackException_IsSwallowed() {
        var attempts = 0;
        using var action = new DebouncedAction(TimeSpan.FromMilliseconds(30), () => {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("boom");
        });

        action.Schedule();
        await WaitUntil(() => Volatile.Read(ref attempts) == 1, TimeSpan.FromSeconds(2));

        action.Schedule();
        await WaitUntil(() => Volatile.Read(ref attempts) == 2, TimeSpan.FromSeconds(2));
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline) {
            if (condition()) {
                return;
            }
            await Task.Delay(10);
        }
    }
}
