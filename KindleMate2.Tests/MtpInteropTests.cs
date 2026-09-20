using System.Runtime.InteropServices;
using KindleMate2.Devices.Posix;
using Xunit;
using Xunit.Abstractions;

namespace KindleMate2.Tests;

/// <summary>
/// libmtp 绑定的 ABI 与可用性用例。
///
/// 为什么值得专门测尺寸:P/Invoke 的结构体布局写错**不会编译失败**,而是运行时静默读到
/// 垃圾字段(甚至写坏内存)。这几个数字是 64 位 C ABI 下逐字段算出来的 —— 顺序错了、
/// 少了个对齐填充,这里就会红。改动 <c>MtpInterop</c> 的字段后请对着 libmtp 头文件重新核算。
///
/// 注意:本文件在**没有 libmtp 的环境**(CI 的 ubuntu/windows 作业)也必须通过 ——
/// 所以只断言"行为可预期",不对"库是否存在"下结论。
/// </summary>
public sealed class MtpInteropTests {
    private readonly ITestOutputHelper _output;

    public MtpInteropTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void StructSizesAndOffsets_MatchCAbi() {
        // 下表的数字是**用 clang 编译真实头文件**算出来的,不是手算 —— 我头一次手算就把前两个
        // 算错了(以为 40/48,实际 32/40),是这条用例把它纠正过来的。复算方法:
        //   printf '#include <stddef.h>\n#include "libmtp.h"\n...' > s.c
        //   clang -I$(brew --prefix libmtp)/include s.c && ./a.out
        // 改动 MtpInterop 的字段后请照此重新核算。

        // LIBMTP_device_entry_t = 32:vendor@0 vendor_id@8 product@16 product_id@24 device_flags@28
        // (device_flags 在 28 而非 32:product_id 之后只按 u32 对齐补 2 字节)
        Assert.Equal(32, Marshal.SizeOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>());
        Assert.Equal(0, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry.Vendor)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry.VendorId)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry.Product)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry.ProductId)).ToInt32());
        Assert.Equal(28, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpDeviceEntry.DeviceFlags)).ToInt32());

        // LIBMTP_raw_device_t = 40:device_entry@0 bus_location@32 devnum@36
        Assert.Equal(40, Marshal.SizeOf<KindleMate2.Devices.Posix.MtpInterop.MtpRawDevice>());
        Assert.Equal(32, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpRawDevice>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpRawDevice.BusLocation)).ToInt32());
        Assert.Equal(36, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpRawDevice>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpRawDevice.Devnum)).ToInt32());

        // LIBMTP_file_t = 56:item_id@0 parent_id@4 storage_id@8 filename@16 filesize@24
        //                    modificationdate@32 filetype@40 next@48
        Assert.Equal(56, Marshal.SizeOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>());
        Assert.Equal(0, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.ItemId)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.ParentId)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.StorageId)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.Filename)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.FileSize)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.ModificationDate)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.FileType)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<KindleMate2.Devices.Posix.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.Posix.MtpInterop.MtpFile.Next)).ToInt32());
    }

    [Fact]
    public void CandidateLibraryPaths_TryBundledCopiesBeforeSystemOnes() {
        var candidates = KindleMate2.Devices.Posix.MtpInterop.CandidateLibraryPaths().ToArray();

        Assert.NotEmpty(candidates);

        // 发布版必须优先用随包分发的库:系统路径在用户机上不一定存在,
        // 而且"随包分发"正是 LGPL 合规声明所对应的那一份。
        // 随包的三处是:程序目录、程序目录下的 lib/、以及 .app 里的 ../Frameworks。
        // AppContext.BaseDirectory **带尾分隔符**、Path.GetDirectoryName 不带 —— 直接比会因末位斜杠而失败
        // (与"别写死路径分隔符"同类的坑:比路径时先归一化形式)。
        Assert.Equal(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            Path.GetDirectoryName(candidates[0]));
        var bundled = candidates.Take(3).ToArray();
        Assert.Contains(bundled, p => Path.GetDirectoryName(p) == Path.Combine(AppContext.BaseDirectory, "lib"));
        Assert.Contains(bundled, p => p.Contains(Path.Combine("..", "Frameworks")));

        // 文件名与系统路径都**按平台**断言 —— 写死 macOS 的 .dylib / /opt/homebrew 会让
        // 这条用例在 Linux 的 CI 作业上假失败(本项目已有过同类教训)。
        if (OperatingSystem.IsMacOS()) {
            Assert.All(candidates, p => Assert.EndsWith(".dylib", p));
            var firstHomebrew = Array.FindIndex(candidates, p => p.StartsWith("/opt/homebrew", StringComparison.Ordinal));
            Assert.True(firstHomebrew > 2, $"Homebrew 路径必须排在随包候选之后(实际位置 {firstHomebrew})");
        } else if (OperatingSystem.IsLinux()) {
            Assert.All(candidates, p => Assert.Contains(".so", p));
            var firstSystem = Array.FindIndex(candidates, p => p.StartsWith("/usr/lib", StringComparison.Ordinal));
            Assert.True(firstSystem > 2, $"系统库路径必须排在随包候选之后(实际位置 {firstSystem})");
        }
    }

    [Fact]
    public void IsAvailable_DoesNotThrow_RegardlessOfWhetherLibraryIsInstalled() {
        // 开发机装了 libmtp、CI 没装 —— 两种情形都必须只是返回一个布尔值,不能抛。
        var available = KindleMate2.Devices.Posix.MtpInterop.IsAvailable();

        if (available) {
            Assert.False(string.IsNullOrEmpty(KindleMate2.Devices.Posix.MtpInterop.LoadedPath));
            Assert.True(File.Exists(KindleMate2.Devices.Posix.MtpInterop.LoadedPath!));
        }
    }

    [Fact]
    public void ErrorNumberDescriptions_CoverKnownCodes() {
        Assert.Contains("没有连接", KindleMate2.Devices.Posix.MtpInterop.MtpError.Describe(5));
        // 未知码也要给出可读描述,而不是抛异常
        Assert.Contains("999", KindleMate2.Devices.Posix.MtpInterop.MtpError.Describe(999));
    }

    [Fact]
    public void InitAndDetect_ReturnDocumentedErrorCode_WhenLibraryIsPresent() {
        // 在没装 libmtp 的环境(CI 的 ubuntu/windows 作业)直接跳过 —— 这里要验的是
        // "绑定能解析、调用能返回",不是"库到处都有"。
        if (!KindleMate2.Devices.Posix.MtpInterop.IsAvailable()) {
            return;
        }

        KindleMate2.Devices.Posix.MtpInterop.LIBMTP_Init();
        var code = KindleMate2.Devices.Posix.MtpInterop.LIBMTP_Detect_Raw_Devices(out var devices, out var count);

        // 关键断言:返回的必须是 libmtp 文档里那个**错误码枚举**里的值。
        // 签名写错(参数宽度、调用约定)通常表现为崩溃或垃圾返回值,而不是乖乖落在 0..8 里。
        // 无设备(5)/USB 层不可用(3,例如沙箱里拿不到 IOKit)都是合法结果 —— 这里不假设环境。
        Assert.InRange(code, KindleMate2.Devices.Posix.MtpInterop.MtpError.None,
            KindleMate2.Devices.Posix.MtpInterop.MtpError.Cancelled);
        Assert.True(count >= 0);

        if (devices != IntPtr.Zero) {
            KindleMate2.Devices.Posix.MtpInterop.LIBMTP_FreeMemory(devices);
        }

        // 便于人工核对:把实测结果写进测试输出(dotnet test --logger "console;verbosity=detailed" 可见)
        _output.WriteLine($"libmtp = {KindleMate2.Devices.Posix.MtpInterop.LoadedPath}");
        _output.WriteLine($"Detect_Raw_Devices -> code={code}({KindleMate2.Devices.Posix.MtpInterop.MtpError.Describe(code)}) count={count}");
    }
}
