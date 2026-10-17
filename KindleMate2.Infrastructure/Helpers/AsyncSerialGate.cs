using System;
using System.Threading;
using System.Threading.Tasks;

namespace KindleMate2.Infrastructure.Helpers;

/// <summary>
/// 把「同一时刻只允许一个异步操作在跑」的闸门封装成可测的小工具。
///
/// 为什么需要:启动迁移被移入 <c>Task.Run</c> 后,UI 在迁移期间可以交互 —— 用户此时切语言会
/// 重建主窗口,新窗口的 <c>Opened</c> 会再跑一次同样的启动迁移,两个迁移并发操作同一个 SQLite
/// 库(<c>ALTER TABLE ... ADD COLUMN</c> 是 check-then-act:都先查列不存在、再都去加)会互相踩,
/// 多出一个 <c>duplicate column name</c> / <c>database is locked</c> 告警。
///
/// 语义:并发调用**排队**而不是并行(不重入);异常照常向调用方抛;异常/取消后闸门必定释放。
/// </summary>
public sealed class AsyncSerialGate {
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>串行执行一个返回值的异步操作。</summary>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 null。</exception>
    public async Task<T> RunAsync<T>(Func<Task<T>> action) {
        ArgumentNullException.ThrowIfNull(action);

        await _gate.WaitAsync().ConfigureAwait(false);
        try {
            return await action().ConfigureAwait(false);
        } finally {
            _gate.Release();
        }
    }

    /// <summary>串行执行一个无返回值的异步操作。</summary>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> 为 null。</exception>
    public Task RunAsync(Func<Task> action) {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(async () => {
            await action().ConfigureAwait(false);
            return true;
        });
    }
}
