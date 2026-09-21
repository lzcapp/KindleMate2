using System.Runtime.InteropServices;
using Xunit;
using KindleMate2.Devices.MacOS;
using KindleMate2.Devices.Posix;

namespace KindleMate2.Tests;

/// <summary>
/// 只读 USB 探测(<c>KindleUsbProbe</c>)的用例 —— 不需要真机。
///
/// 背景:2024 年及以后的 Kindle 在 macOS 上不挂载为磁盘,MTP 是它们唯一的连接方式;
/// 状态栏若只认 /Volumes 就会一直显示"未连接"。补上这一路探测的关键是**它必须无副作用**:
/// 不能开 MTP 会话 —— libmtp 关闭会话时会复位 USB 口(设备条目带 FORCE_RESET_ON_CLOSE),
/// 而状态栏是每 2 秒探一次的。本类只做 libusb 的"取设备列表 + 读描述符"。
/// </summary>
public sealed class KindleUsbProbeTests {
    [Fact]
    public void DescriptorLayout_MatchesCAbi() {
        // 数字由 clang 对着 libusb.h 算出来的(不是手算):全字节/半字字段,64 位下 18 字节无填充。
        // 字段错位不会编译失败,只会把 VID/PID 读成垃圾值 —— 于是"设备插着却显示未连接"。
        Assert.Equal(18, Marshal.SizeOf<KindleUsbProbe.LibusbDeviceDescriptor>());
        Assert.Equal(8, Marshal.OffsetOf<KindleUsbProbe.LibusbDeviceDescriptor>(
            nameof(KindleUsbProbe.LibusbDeviceDescriptor.VendorId)).ToInt32());
        Assert.Equal(10, Marshal.OffsetOf<KindleUsbProbe.LibusbDeviceDescriptor>(
            nameof(KindleUsbProbe.LibusbDeviceDescriptor.ProductId)).ToInt32());
        Assert.Equal(0, Marshal.OffsetOf<KindleUsbProbe.LibusbDeviceDescriptor>(
            nameof(KindleUsbProbe.LibusbDeviceDescriptor.Length)).ToInt32());
        Assert.Equal(17, Marshal.OffsetOf<KindleUsbProbe.LibusbDeviceDescriptor>(
            nameof(KindleUsbProbe.LibusbDeviceDescriptor.ConfigurationCount)).ToInt32());
    }

    [Fact]
    public void DescriptorVendorId_IsReadFromTheDocumentedOffset() {
        // 构造一段 18 字节的原始描述符,VID/PID 放在 8/10 处 —— 若字段偏移错了这里就会读错
        var raw = new byte[18];
        raw[8] = 0x49;
        raw[9] = 0x19;      // idVendor = 0x1949(小端)
        raw[10] = 0x81;
        raw[11] = 0x99;     // idProduct = 0x9981

        var descriptor = MemoryMarshal.Read<KindleUsbProbe.LibusbDeviceDescriptor>(raw);

        Assert.Equal(0x1949, descriptor.VendorId);
        Assert.Equal(0x9981, descriptor.ProductId);
    }

    [Theory]
    [InlineData(0x1949, true)]    // Amazon
    [InlineData(0x1948, false)]
    [InlineData(0x05AC, false)]   // Apple
    [InlineData(0x0000, false)]
    public void IsAmazonVendor_MatchesOnlyTheAmazonVendorId(ushort vendorId, bool expected) {
        Assert.Equal(expected, KindleUsbProbe.IsAmazonVendor(vendorId));
        Assert.Equal(0x1949, KindleUsbProbe.AmazonVendorId);
    }

    [Fact]
    public void LibraryAvailability_IsTrackedPerLibrary() {
        // 回归:曾把"实际加载到的库路径"记在**一个共用字段**里,于是"探测 libusb"
        //(Linux / macOS 上系统自带)会把它的路径写进去,让 MtpInterop.IsAvailable()
        //(判断的是 libmtp)误判为可用,调用方随后在 LIBMTP_Init 上撞 DllNotFoundException。
        // CI 上 ubuntu 与 macOS 两个作业正是这么红的(Windows 没有 POSIX 库反而没事)。
        //
        // ⚠ 本用例**不能**断言"两个记录都非空":Windows 上既没有 libmtp 也没有 libusb,
        // 它们本来就都是 null —— 初版写成了 `Assert.NotEqual(libmtpPath, libusbPath)`,
        // 于是同一个提交 ubuntu/macOS 全绿、只有 Windows 假红(两个 null 被判为相等)。
        // 与平台无关的判据只有一条:**libusb 的加载不得出现在 libmtp 的记录里**。
        // 注意各测试类是并行跑的,别的类可能已合法加载 libmtp,所以只做"不等"比较,
        // 不假定 libmtp 此刻一定未被加载。
        _ = KindleUsbProbe.TryFindKindle(out _);   // 只走 libusb,不会碰 libmtp

        var libmtpPath = MtpInterop.LoadedPath;
        var libusbPath = MtpInterop.LibusbLoadedPath;

        // 各自记录的必须是**自己**那个库
        if (libmtpPath is not null) {
            Assert.Contains("libmtp", Path.GetFileName(libmtpPath), StringComparison.OrdinalIgnoreCase);
        }

        if (libusbPath is not null) {
            Assert.Contains("libusb", Path.GetFileName(libusbPath), StringComparison.OrdinalIgnoreCase);
        }

        // 关键断言:刚枚举过 libusb,libmtp 的记录绝不该变成 libusb 的路径。
        // 共用字段时这里必然相等 —— 也正因如此,这条只在 libusb 真的加载成功时有意义
        //(Windows 上 libusbPath 为 null,跳过:那条路径根本不存在污染源)。
        if (libusbPath is not null) {
            Assert.NotEqual(libusbPath, libmtpPath);
        }
    }

    [Fact]
    public void IsAvailable_And_TryFindKindle_DoNotThrow_RegardlessOfEnvironment() {
        // 开发机有 libusb、CI 上没有 —— 两种情形都只能安静地返回布尔值
        var available = KindleUsbProbe.IsAvailable();
        var found = KindleUsbProbe.TryFindKindle(out var productId);

        if (!found) {
            Assert.Equal(0, productId);
        }
    }
}
