using System.Globalization;
using Microsoft.Data.Sqlite;
using Xunit;
using KindleMate2.Infrastructure.Helpers;

namespace KindleMate2.Tests;

/// <summary>
/// 备份文件名里的时间戳必须是公历日期。
///
/// 回归背景:此前时间戳用不带 culture 的 <c>DateTime.Now.ToString(...)</c>,走的是 CurrentCulture,
/// 而带非公历日历的区域会把年份换成别的历法 —— 实测 th-TH → <c>2569</c>、fa-IR → <c>1405</c>、
/// ar-SA → <c>1448</c>。备份文件名于是读不出真实日期,也无法按名称排序(而"按名字排序取最新一份"
/// 正是用户找备份时最自然的做法)。
///
/// 修法是显式传 <see cref="CultureInfo.InvariantCulture"/>;本文件用非公历区域把它钉住 ——
/// 这类缺陷只在特定区域设置下出现,普通区域(zh-CN / en-US)的用例是抓不到的。
/// </summary>
public sealed class BackupFilenameCultureTests : IDisposable {
    private readonly string _dir;
    private readonly string _dbPath;

    public BackupFilenameCultureTests() {
        _dir = Path.Combine(Path.GetTempPath(), "km2-backupname-tests-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(_dir);

        _dbPath = Path.Combine(_dir, "KM2.dat");
        Assert.True(DatabaseHelper.CreateDatabase(_dbPath, out var ex), "CreateDatabase failed: " + ex.Message);
    }

    public void Dispose() {
        try {
            // 刻意**不**在这里清 SQLite 连接池:那是进程级 API,会和并行跑的其他测试类互撞
            // (实测 ~1/6 概率报 ObjectDisposedException)。残留目录由 TestTempCleanup 在
            // 进程退出时统一清扫 —— 那里已无测试在跑,清池不会伤到谁。
            Directory.Delete(_dir, true);
        } catch { /* best effort */ }
    }

    [Theory]
    [InlineData("th-TH")]   // 佛历:年份会变成 2569
    [InlineData("fa-IR")]   // 波斯历:1405
    [InlineData("ar-SA")]   // 回历:1448
    [InlineData("zh-CN")]   // 公历(对照组)
    [InlineData("en-US")]   // 公历(对照组)
    public void BackupDatabase_FileNameKeepsGregorianYear_UnderAnyCulture(string cultureName) {
        var backupDir = Path.Combine(_dir, "Backups");
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            DatabaseHelper.BackupDatabase(_dir, backupDir, Path.GetFileName(_dbPath));
        } finally {
            CultureInfo.CurrentCulture = original;
        }

        var fileName = Path.GetFileName(Assert.Single(Directory.GetFiles(backupDir)));

        // 只断言"含当前公历年",不比整串 —— 免得在午夜前后跨秒时抖动
        var gregorianYear = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
        Assert.Contains(gregorianYear, fileName);
        Assert.Matches(@"\d{8}_\d{6}", fileName);
    }
}
