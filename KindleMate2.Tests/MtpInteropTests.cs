using System.Runtime.InteropServices;
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
        Assert.Equal(32, Marshal.SizeOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>());
        Assert.Equal(0, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry.Vendor)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry.VendorId)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry.Product)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry.ProductId)).ToInt32());
        Assert.Equal(28, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpDeviceEntry.DeviceFlags)).ToInt32());

        // LIBMTP_raw_device_t = 40:device_entry@0 bus_location@32 devnum@36
        Assert.Equal(40, Marshal.SizeOf<KindleMate2.Devices.MacOS.MtpInterop.MtpRawDevice>());
        Assert.Equal(32, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpRawDevice>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpRawDevice.BusLocation)).ToInt32());
        Assert.Equal(36, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpRawDevice>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpRawDevice.Devnum)).ToInt32());

        // LIBMTP_file_t = 56:item_id@0 parent_id@4 storage_id@8 filename@16 filesize@24
        //                    modificationdate@32 filetype@40 next@48
        Assert.Equal(56, Marshal.SizeOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>());
        Assert.Equal(0, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.ItemId)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.ParentId)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.StorageId)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.Filename)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.FileSize)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.ModificationDate)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.FileType)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<KindleMate2.Devices.MacOS.MtpInterop.MtpFile>(nameof(KindleMate2.Devices.MacOS.MtpInterop.MtpFile.Next)).ToInt32());
    }

    [Fact]
    public void CandidateLibraryPaths_TryBundledCopiesBeforeHomebrew() {
        var candidates = KindleMate2.Devices.MacOS.MtpInterop.CandidateLibraryPaths().ToArray();

        // 发布版必须优先用随包分发的库:Homebrew 的路径在用户机上不一定存在,
        // 而且"随包分发"正是 LGPL 合规声明所对应的那份。
        Assert.EndsWith("libmtp.9.dylib", candidates[0]);
        Assert.Contains("Frameworks", candidates[1]);

        var firstHomebrew = Array.FindIndex(candidates, p => p.StartsWith("/opt/homebrew", StringComparison.Ordinal));
        Assert.True(firstHomebrew > 1, "Homebrew 的路径必须排在随包分发的候选之后");
    }

    [Fact]
    public void IsAvailable_DoesNotThrow_RegardlessOfWhetherLibraryIsInstalled() {
        // 开发机装了 libmtp、CI 没装 —— 两种情形都必须只是返回一个布尔值,不能抛。
        var available = KindleMate2.Devices.MacOS.MtpInterop.IsAvailable();

        if (available) {
            Assert.False(string.IsNullOrEmpty(KindleMate2.Devices.MacOS.MtpInterop.LoadedPath));
            Assert.True(File.Exists(KindleMate2.Devices.MacOS.MtpInterop.LoadedPath!));
        }
    }

    [Fact]
    public void ErrorNumberDescriptions_CoverKnownCodes() {
        Assert.Contains("没有连接", KindleMate2.Devices.MacOS.MtpInterop.MtpError.Describe(5));
        // 未知码也要给出可读描述,而不是抛异常
        Assert.Contains("999", KindleMate2.Devices.MacOS.MtpInterop.MtpError.Describe(999));
    }

    [Fact]
    public void InitAndDetect_ReturnDocumentedErrorCode_WhenLibraryIsPresent() {
        // 在没装 libmtp 的环境(CI 的 ubuntu/windows 作业)直接跳过 —— 这里要验的是
        // "绑定能解析、调用能返回",不是"库到处都有"。
        if (!KindleMate2.Devices.MacOS.MtpInterop.IsAvailable()) {
            return;
        }

        KindleMate2.Devices.MacOS.MtpInterop.LIBMTP_Init();
        var code = KindleMate2.Devices.MacOS.MtpInterop.LIBMTP_Detect_Raw_Devices(out var devices, out var count);

        // 关键断言:返回的必须是 libmtp 文档里那个**错误码枚举**里的值。
        // 签名写错(参数宽度、调用约定)通常表现为崩溃或垃圾返回值,而不是乖乖落在 0..8 里。
        // 无设备(5)/USB 层不可用(3,例如沙箱里拿不到 IOKit)都是合法结果 —— 这里不假设环境。
        Assert.InRange(code, KindleMate2.Devices.MacOS.MtpInterop.MtpError.None,
            KindleMate2.Devices.MacOS.MtpInterop.MtpError.Cancelled);
        Assert.True(count >= 0);

        if (devices != IntPtr.Zero) {
            KindleMate2.Devices.MacOS.MtpInterop.LIBMTP_FreeMemory(devices);
        }

        // 便于人工核对:把实测结果写进测试输出(dotnet test --logger "console;verbosity=detailed" 可见)
        _output.WriteLine($"libmtp = {KindleMate2.Devices.MacOS.MtpInterop.LoadedPath}");
        _output.WriteLine($"Detect_Raw_Devices -> code={code}({KindleMate2.Devices.MacOS.MtpInterop.MtpError.Describe(code)}) count={count}");
    }
}
