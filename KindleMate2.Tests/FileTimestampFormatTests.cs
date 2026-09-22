using System.Globalization;
using Xunit;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="AppConstants.FileTimestampFormat"/> 的两条不变量。
///
/// 这个格式串此前散在**五个地方**(两个常量 + 三处硬编码字面量:库备份 / 维护清单 /
/// 导入文件 / 清空前保底备份 / 统计截图),2026-09-22 收敛成一个。本文件把
/// "为什么不能随便改"钉进测试里,而不是只写在注释里 —— **注释不会红,测试会**。
///
/// 配套的 CI 源码断言管另外两件事:不许再有地方硬编码这个格式串;
/// 每个用点都必须配 <c>InvariantCulture</c>。
/// </summary>
public sealed class FileTimestampFormatTests {

    /// <summary>
    /// 定长零填充 ⇒ **字典序 = 时间序**。
    ///
    /// 这条不是审美偏好:<c>PruneBackups</c> 的保留策略按**文件名字典序**判定新旧
    /// (刻意不用 mtime —— 复制 / 云同步 / 解压都会重写 mtime)。
    /// 格式一旦丢掉零填充(例如把 <c>MM</c> 写成 <c>M</c>),长度就随日期变化,
    /// 字典序立刻不再是时间序,清理会**删错份**。
    /// </summary>
    [Fact]
    public void Format_KeepsLexicographicOrderEqualToChronologicalOrder() {
        var moments = new[] {
            new DateTime(2017, 1, 2, 3, 4, 5),      // 月/日/时/分/秒都是个位数:零填充的试金石
            new DateTime(2017, 6, 11, 6, 57, 28),
            new DateTime(2017, 9, 1, 0, 0, 0),
            new DateTime(2018, 1, 1, 0, 0, 0),      // 跨年:年份位数/顺序
            new DateTime(2099, 12, 31, 23, 59, 59)
        };

        var stamps = moments
            .Select(m => m.ToString(AppConstants.FileTimestampFormat, CultureInfo.InvariantCulture))
            .ToList();

        // ① 定长(零填充到位)
        Assert.Single(stamps.Select(s => s.Length).Distinct());
        // ② 字典序 = 时间序
        Assert.Equal(stamps, stamps.OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// 精确形状:字段顺序与分隔符一起钉住 —— 免得"顺序悄悄换了"却仍然定长
    /// (那样 ① 那条还是绿的,而文件名会变得读不出日期)。
    /// </summary>
    [Fact]
    public void Format_HasExpectedShape() {
        var value = new DateTime(2017, 6, 11, 6, 57, 28)
            .ToString(AppConstants.FileTimestampFormat, CultureInfo.InvariantCulture);

        Assert.Equal("20170611_065728", value);
    }
}
