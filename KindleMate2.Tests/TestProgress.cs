using KindleMate2.Application.Models;

namespace KindleMate2.Tests;

/// <summary>
/// 同步的进度接收器,供测试捕获阶段序列。
/// 测试里**不能**用 <see cref="Progress{T}"/>:它在没有同步上下文时是异步投递的,
/// 断言会跑在回调之前(与 <c>--ops</c> 自检里 <c>SyncProgress</c> 的取舍一致)。
/// </summary>
internal sealed class TestProgress(Action<OperationProgress> handler) : IProgress<OperationProgress> {
    public void Report(OperationProgress value) => handler(value);
}
