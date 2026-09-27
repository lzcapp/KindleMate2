using System.IO;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Application.Models;

/// <summary>
/// 设备概览 —— 关于窗口「设备」段的数据源。
///
/// 两条铁律:
/// ① **逐项可空**:字段拿不到就留 null(拔线竞态、MTP 未导入过、平台不支持…),
///    UI 侧按"非空才显示该行"渲染,不摆「—」占位。
/// ② **绝不开会话**:所有取值只能来自卷文件读取、<c>DriveInfo</c> 与既有缓存 ——
///    这条继承自 <c>PosixDeviceManager.GetKindleVersionText</c> 的注释:
///    getter 可能在任意时机被界面调用,而每次 MTP 会话都要重新打开设备。
/// </summary>
public sealed record DeviceSummary(
    Device.Type Type,
    string? DrivePath,
    string? Firmware,
    long? TotalBytes,
    long? FreeBytes) {

    /// <summary>
    /// USB 文件卷的摘要:路径与固件照传,容量用 <see cref="DriveInfo"/> 现读。
    /// 容量读失败(卷刚被拔、无权限…)**不算错** —— 容量字段留空,UI 隐藏该行即可,
    /// 路径与固件仍然有效。
    /// </summary>
    public static DeviceSummary FromUsb(string drivePath, string? firmware) {
        long? total = null;
        long? free = null;
        try {
            var drive = new DriveInfo(drivePath);
            total = drive.TotalSize;
            free = drive.AvailableFreeSpace;
        } catch {
            // 空卷已拔/路径失效/权限不足 —— 容量不可得,行隐藏
        }
        return new DeviceSummary(Device.Type.USB, drivePath, SingleLine(firmware), total, free);
    }

    /// <summary>
    /// <c>version.txt</c> 可能是多行(机型行 / 版本行 / 构建号…),「固件」行只展示**第一行非空行**;
    /// 全空返回 null(行隐藏)。
    /// </summary>
    public static string? SingleLine(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return null;
        }
        foreach (var line in text.Split('\n')) {
            var trimmed = line.Trim();
            if (trimmed.Length > 0) {
                return trimmed;
            }
        }
        return null;
    }
}
