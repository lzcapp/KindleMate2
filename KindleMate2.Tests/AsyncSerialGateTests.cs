using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="AsyncSerialGate"/> 的回归用例。
///
/// 背景:启动迁移被移入后台线程后,「用户在迁移期间切语言 → 重建窗口 → 新窗口的 OnOpened
/// 再跑一次 PrepareDatabaseAsync」会让两个迁移并发操作同一个 SQLite 库(ALTER TABLE 是
/// check-then-act)。VM 层单测够不着(测试工程不引 Avalonia),所以把闸门抽成可测工具在本文件钉住:
/// 并发调用必须串行、异常必须释放闸门、返回值必须透传。
/// </summary>
public sealed class AsyncSerialGateTests {
    [Fact]
    public async Task ConcurrentCalls_AreSerialized() {
        var gate = new AsyncSerialGate();
        var firstEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = 0;
        var inside = 0;
        var maxInside = 0;

        var first = gate.RunAsync(async () => {
            var now = Interlocked.Increment(ref inside);
            maxInside = Math.Max(maxInside, now);
            firstEntered.SetResult(true);
            await releaseFirst.Task;
            Interlocked.Decrement(ref inside);
            return 1;
        });

        await firstEntered.Task;

        var second = gate.RunAsync(async () => {
            var now = Interlocked.Increment(ref inside);
            maxInside = Math.Max(maxInside, now);
            Interlocked.Exchange(ref secondEntered, 1);
            Interlocked.Decrement(ref inside);
            return 2;
        });

        // 第一个被 releaseFirst 卡住时,第二个必须还在等闸门 —— 若闸门失效它此刻就已进入。
        await Task.Delay(50);
        Assert.Equal(0, Volatile.Read(ref secondEntered));

        releaseFirst.SetResult(true);
        await Task.WhenAll(first, second);

        Assert.Equal(1, maxInside);                    // 两次执行从未重叠
        Assert.Equal(1, Volatile.Read(ref secondEntered));
        Assert.Equal(1, await first);
        Assert.Equal(2, await second);
    }

    [Fact]
    public async Task WhenActionThrows_ExceptionPropagates_AndGateIsReleased() {
        var gate = new AsyncSerialGate();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            gate.RunAsync<int>(() => throw new InvalidOperationException("迁移失败")));

        // 闸门必须已释放:紧接着的下一次调用不能卡死。
        var result = await gate.RunAsync(() => Task.FromResult(42));
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ReturnsActionResult() {
        var gate = new AsyncSerialGate();
        var value = await gate.RunAsync(async () => {
            await Task.Yield();
            return "done";
        });
        Assert.Equal("done", value);
    }

    [Fact]
    public async Task NonGenericOverload_RunsToCompletion() {
        var gate = new AsyncSerialGate();
        var ran = false;
        await gate.RunAsync(async () => {
            await Task.Yield();
            ran = true;
        });
        Assert.True(ran);
    }
}
