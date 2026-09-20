using Xunit;
using KindleMate2.Devices.Linux;
using KindleMate2.Shared.Constants;
using KindleMate2.Shared.Entities;

namespace KindleMate2.Tests;

/// <summary>
/// Linux 设备实现的用例 —— 不需要真机。
///
/// Linux 与 macOS 共用 <c>KindleMate2.Devices.Posix</c> 里的实现,唯一的差别是**卷挂在哪里**:
/// Linux 没有统一挂载点,桌面环境通常挂在 <c>/media/&lt;用户&gt;</c> 或 <c>/run/media/&lt;用户&gt;</c>,
/// 老式/手工挂载则可能在 <c>/media</c>、<c>/mnt</c>。所以本文件重点验两件事:
/// ① 候选根列表覆盖这些常见位置;② **多个候选根会被依次扫**(这是为 Linux 新写的逻辑,
/// macOS 只有 /Volumes 一个根,碰不到)。
/// 卷判定/导入的细节逻辑由 <c>MacOSDeviceManagerTests</c> 覆盖(同一份实现)。
/// </summary>
public sealed class LinuxDeviceManagerTests : IDisposable {
    private const string VersionFileRelativePath = "system/version.txt";

    /// <summary>两个候选根,模拟 <c>/media/&lt;用户&gt;</c> 与 <c>/run/media/&lt;用户&gt;</c>。</summary>
    private readonly string _firstRoot;
    private readonly string _secondRoot;

    public LinuxDeviceManagerTests() {
        var baseDir = Path.Combine(Path.GetTempPath(), "km2-linux-roots-" + Guid.NewGuid().ToString("N")[..10]);
        _firstRoot = Path.Combine(baseDir, "media-user");
        _secondRoot = Path.Combine(baseDir, "run-media-user");
        Directory.CreateDirectory(_firstRoot);
        Directory.CreateDirectory(_secondRoot);
    }

    public void Dispose() {
        try { Directory.Delete(Path.GetDirectoryName(_firstRoot)!, true); } catch { /* best effort */ }
    }

    [Fact]
    public void DefaultVolumeRoots_CoverCommonLinuxMountPoints() {
        var roots = KindleMate2.Devices.Linux.DeviceManager.DefaultVolumeRoots;

        // 桌面环境把可移动卷挂在用户名之下(两种 udisks2 行为),另有老式与手工挂载
        Assert.Contains(Path.Combine("/media", Environment.UserName), roots);
        Assert.Contains(Path.Combine("/run/media", Environment.UserName), roots);
        Assert.Contains("/media", roots);
        Assert.Contains("/mnt", roots);
    }

    [Fact]
    public void NoVolume_ReportsNotConnected() {
        using var manager = CreateManager();

        Assert.False(manager.IsKindleConnected());
        Assert.Equal(string.Empty, manager.DriveLetter);
    }

    [Fact]
    public void KindleInSecondRoot_IsFound() {
        // 这是 Linux 特有的情形:卷不在第一个候选根下,必须继续往下扫
        MountKindle(_secondRoot);
        using var manager = CreateManager();

        Assert.True(manager.IsKindleConnected());
        Assert.Equal(Path.Combine(_secondRoot, "Kindle"), manager.DriveLetter);
        Assert.Equal(Device.Type.USB, manager.DeviceType);
    }

    [Fact]
    public void KindleInFirstRoot_IsPreferredOverLaterRoots() {
        MountKindle(_firstRoot);
        MountKindle(_secondRoot);
        using var manager = CreateManager();

        Assert.True(manager.IsKindleConnected());
        // 按候选顺序取第一个命中:/media/<用户> 排在 /run/media/<用户> 之前
        Assert.Equal(Path.Combine(_firstRoot, "Kindle"), manager.DriveLetter);
    }

    [Fact]
    public void VolumeWithoutClippings_IsNotMistakenForKindle() {
        // 别的 U 盘:有 documents 目录但没有 My Clippings.txt
        Directory.CreateDirectory(Path.Combine(_secondRoot, "SanDisk", "documents"));
        using var manager = CreateManager();

        Assert.False(manager.IsKindleConnected());
    }

    private KindleMate2.Devices.Linux.DeviceManager CreateManager() =>
        new(VersionFileRelativePath, [_firstRoot, _secondRoot], detectMtpDevices: false);

    private static void MountKindle(string root) {
        var volume = Path.Combine(root, "Kindle");
        Directory.CreateDirectory(Path.Combine(volume, AppConstants.DocumentsPathName));
        Directory.CreateDirectory(Path.Combine(volume, AppConstants.SystemPathName, AppConstants.VocabularyPathName));
        File.WriteAllText(Path.Combine(volume, AppConstants.SystemPathName, AppConstants.VersionFileName), "Kindle 5.16.2\n");
        File.WriteAllText(Path.Combine(volume, AppConstants.DocumentsPathName, AppConstants.ClippingsFileName), "clippings-body");
    }
}
