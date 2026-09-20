using System.Runtime.InteropServices;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Devices.MacOS;

/// <summary>
/// 只读地判断"USB 上有没有接着 Amazon 的设备" —— 用来让状态栏能反映 MTP 机型
/// (2024 年及以后的 Kindle 在 macOS 上不挂载为磁盘,只认 /Volumes 会一直显示"未连接")。
///
/// **为什么用 libusb 而不是 libmtp 的 <c>LIBMTP_Detect_Raw_Devices</c>**:后者内部也会去开设备,
/// 而我们实测过——**每开一次 MTP 会话,libmtp 就会在关闭时复位 USB 口**
/// (设备条目带 `DEVICE_FLAG_FORCE_RESET_ON_CLOSE`,见 libusb1-glue.c 的 close_usb),
/// 结果是设备重新枚举、macOS 再弹一次「允许配件连接?」。状态栏是每 2 秒探一次的,
/// 绝不能挂在这种有副作用的调用上。
///
/// 本类只做"init → 取设备列表 → 读描述符 → 释放",**不 open、不 claim 接口**,
/// 因此不改变设备状态、不需要授权、也不影响正在进行的 MTP 会话。
/// </summary>
internal static class KindleUsbProbe {
    /// <summary>Amazon 的 USB 厂商 ID(Kindle / Fire 全系都用它)。</summary>
    internal const ushort AmazonVendorId = 0x1949;

    static KindleUsbProbe() {
        // 本类自己发起 DllImport,必须先确保程序集的解析器已注册 —— 否则 library 只会按
        // dyld 默认路径找,/opt/homebrew/lib 与 .app 内的 Frameworks 都不在其列,
        // 结果就是"库明明装着却报不可用"(实测踩过)。
        MtpInterop.EnsureLibraryResolver();
    }

    /// <summary>VID 是否属于 Amazon —— 纯函数,便于单测。</summary>
    internal static bool IsAmazonVendor(ushort vendorId) => vendorId == AmazonVendorId;

    /// <summary>libusb 能否用(未随包也没装时返回 false,不抛)。</summary>
    internal static bool IsAvailable() {
        try {
            return libusb_init(out var context) == 0 && ReleaseContext(context);
        } catch (DllNotFoundException) {
            return false;
        } catch (EntryPointNotFoundException) {
            return false;
        }
    }

    /// <summary>
    /// 是否存在 Amazon 设备。找到时通过 <paramref name="productId"/> 给出产品 ID(便于诊断),
    /// 任何失败都当作"没找到"——状态探测不该把主流程炸掉。
    /// </summary>
    internal static bool TryFindKindle(out ushort productId) {
        productId = 0;

        IntPtr context = IntPtr.Zero;
        IntPtr deviceList = IntPtr.Zero;
        try {
            if (libusb_init(out context) != 0) {
                return false;
            }

            var count = libusb_get_device_list(context, out deviceList).ToInt64();
            if (count <= 0 || deviceList == IntPtr.Zero) {
                return false;
            }

            for (long i = 0; i < count; i++) {
                var device = Marshal.ReadIntPtr(deviceList, (int)(i * IntPtr.Size));
                if (libusb_get_device_descriptor(device, out var descriptor) != 0) {
                    continue;
                }

                if (IsAmazonVendor(descriptor.VendorId)) {
                    productId = descriptor.ProductId;
                    return true;
                }
            }

            return false;
        } catch (DllNotFoundException) {
            // 用户机上既没随包也没装 libusb:当作没有 MTP 设备(USB 卷那条路径照常工作)
            return false;
        } catch (EntryPointNotFoundException) {
            return false;
        } catch (Exception ex) {
            AppLog.Write($"[KindleUsbProbe] 枚举 USB 失败:{ex}");
            return false;
        } finally {
            if (deviceList != IntPtr.Zero) {
                libusb_free_device_list(deviceList, 1);
            }

            ReleaseContext(context);
        }
    }

    private static bool ReleaseContext(IntPtr context) {
        if (context == IntPtr.Zero) {
            return false;
        }

        libusb_exit(context);
        return true;
    }

    // ————————————————————— 绑定(用法见 libusb.h) —————————————————————

    /// <summary><c>int libusb_init(libusb_context **ctx);</c> 0 为成功。</summary>
    [DllImport(MtpInterop.LibusbLibraryName)]
    private static extern int libusb_init(out IntPtr context);

    /// <summary><c>ssize_t libusb_get_device_list(libusb_context *ctx, libusb_device ***list);</c> 返回设备数,负数出错。</summary>
    [DllImport(MtpInterop.LibusbLibraryName)]
    private static extern IntPtr libusb_get_device_list(IntPtr context, out IntPtr list);

    /// <summary><c>int libusb_get_device_descriptor(libusb_device *dev, struct libusb_device_descriptor *desc);</c></summary>
    [DllImport(MtpInterop.LibusbLibraryName)]
    private static extern int libusb_get_device_descriptor(IntPtr device, out LibusbDeviceDescriptor descriptor);

    /// <summary><c>void libusb_free_device_list(libusb_device **list, int unref_devices);</c></summary>
    [DllImport(MtpInterop.LibusbLibraryName)]
    private static extern void libusb_free_device_list(IntPtr list, int unrefDevices);

    /// <summary><c>void libusb_exit(libusb_context *ctx);</c></summary>
    [DllImport(MtpInterop.LibusbLibraryName)]
    private static extern void libusb_exit(IntPtr context);

    /// <summary>
    /// <c>struct libusb_device_descriptor</c> —— 全字节/半字字段,64 位下 18 字节无填充。
    /// 布局同样用 clang 对着 libusb.h 核算过(size 18;bLength@0 … bNumConfigurations@17),
    /// 并由单测钉住:字段错位不会编译失败,只会读到垃圾 VID。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct LibusbDeviceDescriptor {
        public byte Length;                  // bLength
        public byte DescriptorType;          // bDescriptorType
        public ushort UsbVersion;            // bcdUSB
        public byte DeviceClass;             // bDeviceClass
        public byte DeviceSubClass;          // bDeviceSubClass
        public byte DeviceProtocol;          // bDeviceProtocol
        public byte MaxPacketSize0;          // bMaxPacketSize0
        public ushort VendorId;              // idVendor
        public ushort ProductId;             // idProduct
        public ushort DeviceVersion;         // bcdDevice
        public byte ManufacturerIndex;       // iManufacturer
        public byte ProductIndex;            // iProduct
        public byte SerialNumberIndex;       // iSerialNumber
        public byte ConfigurationCount;      // bNumConfigurations
    }
}
