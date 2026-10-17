using System;
using System.IO;
using System.Linq;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="AtomicFile.WriteAllText"/> 的回归用例。
///
/// 背景:应用设置(主题 / 语言 / 跳过版本)此前用 <c>File.WriteAllText</c> 直接覆写
/// <c>settings.json</c>。进程在写到一半时崩溃/被杀、或两处并发写,都会留下**半截 JSON**;
/// 而 <c>AppSettings.Load</c> 读不出来就静默回退默认值 —— 用户的选择被无声丢掉,
/// 现象是"设置莫名其妙没了"。
///
/// 这里钉住原子写的四条边界:<list type="number">
///   <item>目标目录不存在时自动创建并写入正确内容;</item>
///   <item>覆盖写替换为**新**内容(不是追加、不是保留旧内容);</item>
///   <item>成功后不留 GUID 命名的临时文件(Move 掉了才算数);</item>
///   <item>替换失败时抛出、且**不留**临时文件(清理路径),是否容错交由调用方决定。</item>
/// </list>
///
/// 说明:<c>AppSettings</c> 自身在 Avalonia 工程,测试工程刻意不引用该工程,故无法直接覆盖;
/// 但 <c>AppSettings.Save</c> 已改为只委托本 helper,机制由本组用例覆盖。
/// </summary>
public sealed class AtomicFileTests : IDisposable {
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "km2-atomic-tests-" + Guid.NewGuid().ToString("N"));

    public AtomicFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose() {
        // Windows 上若句柄尚未释放,删除会失败 —— 留给进程退出的清扫兜底,别让用例变红。
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    [Fact]
    public void WriteAllText_CreatesMissingDirectory_AndWritesContent() {
        var path = Path.Combine(_dir, "nested", "settings.json");

        AtomicFile.WriteAllText(path, "{\"a\":1}");

        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_OverwritesExistingContent() {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "old-content");

        AtomicFile.WriteAllText(path, "new-content");

        Assert.Equal("new-content", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_LeavesNoTempFileBehind() {
        var path = Path.Combine(_dir, "settings.json");

        AtomicFile.WriteAllText(path, "content");

        // 目录里只应剩目标文件本身 —— GUID 命名的 .tmp 必须被 Move 掉,而不是留在原地。
        var files = Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "settings.json" }, files);
    }

    [Fact]
    public void WriteAllText_WhenReplacementFails_Throws_AndLeavesNoTempFile() {
        // 目标位置先占成一个目录 → File.Move(temp, 目录, overwrite: true) 必失败。
        var path = Path.Combine(_dir, "settings.json");
        Directory.CreateDirectory(path);

        var threw = false;
        try {
            AtomicFile.WriteAllText(path, "content");
        } catch {
            threw = true;
        }

        Assert.True(threw, "目标不可替换时应向上抛出,由调用方(设置保存)决定是否容错");
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }
}
