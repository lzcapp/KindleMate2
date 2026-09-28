using KindleMate2.Application.Models;
using KindleMate2.Shared.Entities;
using Xunit;

namespace KindleMate2.Tests;

/// <summary>
/// <see cref="DeviceSummary"/> —— 关于窗口「设备」段的数据约定:
/// 字段逐项可空,拿不到就留 null(UI 不显示该行,**不摆「—」占位**);
/// 固件只取 <c>version.txt</c> 的第一行非空行。
/// </summary>
public sealed class DeviceSummaryTests {

    [Fact]
    public void SingleLine_ReturnsFirstNonEmptyLine() {
        Assert.Equal("Kindle 5.16.2", DeviceSummary.SingleLine("Kindle 5.16.2\n515102000\n"));
        Assert.Equal("5.16.2", DeviceSummary.SingleLine("\n\n  \n 5.16.2 \nbuild\n"));
    }

    [Fact]
    public void SingleLine_BlankOrNull_ReturnsNull_SoRowStaysHidden() {
        Assert.Null(DeviceSummary.SingleLine(null));
        Assert.Null(DeviceSummary.SingleLine(string.Empty));
        Assert.Null(DeviceSummary.SingleLine("   \n \t \n"));
    }

    [Fact]
    public void FromUsb_FillsTypePathAndFirstLineFirmware() {
        var tempRoot = Path.GetTempPath();

        var summary = DeviceSummary.FromUsb(tempRoot, "Kindle 5.16.2\ndevice details\n");

        Assert.Equal(Device.Type.USB, summary.Type);
        Assert.Equal(tempRoot, summary.DrivePath);
        Assert.Equal("Kindle 5.16.2", summary.Firmware);
    }

    [Fact]
    public void FromUsb_FirmwareTakesFirstLine_MultiLineVersionFile() {
        var summary = DeviceSummary.FromUsb(Path.GetTempPath(), "line-one\nline-two\nline-three");

        Assert.Equal("line-one", summary.Firmware);
    }

    [Fact]
    public void FromUsb_BlankFirmware_IsNull_NotEmptyString() {
        // 空串会让 UI 的"非空才显示"判断误显示一个空行,必须归一成 null
        var summary = DeviceSummary.FromUsb(Path.GetTempPath(), "  \n");

        Assert.Null(summary.Firmware);
    }

    [Fact]
    public void FromUsb_TempRoot_CapacityFromDriveInfo_AndSelfConsistent() {
        var summary = DeviceSummary.FromUsb(Path.GetTempPath(), "1.0.0");

        Assert.NotNull(summary.TotalBytes);
        Assert.NotNull(summary.FreeBytes);
        Assert.True(summary.TotalBytes > 0);
        Assert.InRange(summary.FreeBytes!.Value, 0, summary.TotalBytes!.Value);
    }
}
