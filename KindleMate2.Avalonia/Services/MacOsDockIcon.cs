#if MACOS
using System;
using System.IO;
using System.Runtime.InteropServices;
using KindleMate2.Shared.Diagnostics;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 在 macOS 上把 **Dock 图标**设成应用自己的图标。
///
/// <para>
/// **为什么需要**:macOS 的 Dock 图标来自 <b>.app bundle</b> 的 <c>CFBundleIconFile</c>
/// (发布流程里由 sips/iconutil 生成 .icns 塞进 <c>Contents/Resources</c>,见 release.yml)。
/// 而开发时跑的是**裸 dll**(Rider / <c>dotnet run</c>),没有 bundle ⇒ Dock 只能显示
/// 可执行文件(也就是 <c>dotnet</c>)的图标 —— 看起来像命令行程序。
/// </para>
/// <para>
/// **与 <c>Window.Icon</c> 无关**:后者在 macOS 上设的是 NSWindow 的图标,不是 Dock 的。
/// 所以窗口 XAML 里写了 <c>Icon="/Assets/bookmark.ico"</c> 也改变不了 Dock。
/// </para>
/// <para>
/// 做法:用 Objective-C runtime 直接调 <c>NSApplication.setApplicationIconImage:</c>。
/// 必须在 Avalonia 初始化完成**之后**调用 —— 那时 NSApplication 才存在
/// (见 <c>App.OnFrameworkInitializationCompleted</c>)。
/// </para>
/// <para>
/// 纯 P/Invoke,不依赖 Avalonia —— 资源的取得与落地由调用方负责。
/// </para>
/// </summary>
internal static class MacOsDockIcon {
    /// <summary>把 <paramref name="pngPath"/> 设为 Dock 图标。任何失败都只写日志,绝不抛。</summary>
    internal static void Apply(string? pngPath) {
        if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath)) {
            return;
        }

        try {
            // NSString.stringWithUTF8String:
            var nsStringClass = ObjcGetClass("NSString");
            var stringWithUtf8 = SelRegisterName("stringWithUTF8String:");
            var pathPtr = Marshal.StringToHGlobalAnsi(pngPath);
            IntPtr nsPath;
            try {
                nsPath = MsgSend1(nsStringClass, stringWithUtf8, pathPtr);
            } finally {
                Marshal.FreeHGlobal(pathPtr);
            }

            // NSImage.alloc / initWithContentsOfFile:
            var nsImageClass = ObjcGetClass("NSImage");
            var image = MsgSend1(MsgSend0(nsImageClass, SelRegisterName("alloc")),
                SelRegisterName("initWithContentsOfFile:"), nsPath);
            if (image == IntPtr.Zero) {
                AppLog.Write($"[MacOsDockIcon] NSImage 加载失败:{pngPath}");
                return;
            }

            // NSApplication.sharedApplication / setApplicationIconImage:
            var nsAppClass = ObjcGetClass("NSApplication");
            var sharedApp = MsgSend0(nsAppClass, SelRegisterName("sharedApplication"));
            if (sharedApp == IntPtr.Zero) {
                AppLog.Write("[MacOsDockIcon] 拿不到 NSApplication.sharedApplication");
                return;
            }

            MsgSend1(sharedApp, SelRegisterName("setApplicationIconImage:"), image);
        } catch (Exception ex) {
            // Dock 图标只是外观:失败绝不能影响启动
            AppLog.Write($"[MacOsDockIcon] 设置 Dock 图标失败:{ex.Message}");
        }
    }

    // ————————————— Objective-C runtime 绑定 —————————————
    //
    // ⚠️ objc_msgSend 在 arm64 上**不能**声明成可变参数 —— Apple 的 ABI 要求每个参数个数
    //    一个精确签名(可变参数版本在 arm64 上会读错寄存器)。所以下面按 0/1 个参数各声明一个。

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr ObjcGetClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr SelRegisterName(string name);

    /// <summary><c>id objc_msgSend(id self, SEL op)</c></summary>
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend0(IntPtr receiver, IntPtr selector);

    /// <summary><c>id objc_msgSend(id self, SEL op, id arg1)</c></summary>
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr MsgSend1(IntPtr receiver, IntPtr selector, IntPtr arg1);
}
#endif
