using System;
using System.IO;
using KindleMate2.Shared.Constants;

namespace KindleMate2.Avalonia.Services;

/// <summary>
/// 应用的数据根目录,以及由它派生的各条路径 —— **全进程唯一的定义处**。
///
/// 为什么单独提出来:此前有六处各自写 <c>Path.Combine(Environment.CurrentDirectory, ...)</c>
/// (默认库、Backups、日志、退出备份、启动恢复检查…),口径只靠"大家都记得用当前目录"维持 ——
/// 漏一处就会指向别处(备份写到一个目录、提示打开另一个目录),而且没有任何一处能说明
/// "当前目录为什么就是数据目录"。
///
/// 数据目录 = 进程当前目录,这是原版「程序目录即数据目录」的延续:
/// <list type="bullet">
///   <item>Windows:从资源管理器双击启动时,当前目录就是 exe 所在目录,与旧版数据位置一致(不擅自搬家);</item>
///   <item>macOS / Linux:发布包的启动器会先 <c>cd</c> 到用户数据目录再 exec 真程序。
///         绝不能改用 exe 所在目录 —— 在 macOS 上那是 <c>.app</c> 内部,写入会破坏签名。</item>
/// </list>
///
/// **刻意不缓存**:自检 / 运维模式为了不碰真实数据会临时切换当前目录
/// (见 <c>Program.cs</c> 的 <c>--smoke</c> / <c>--operations</c>),一旦缓存,
/// 后续路径就会继续指向切换前的旧目录。
/// </summary>
public static class AppPaths {
    /// <summary>数据根目录。语义见类型注释:等于进程当前目录。</summary>
    public static string DataDirectory => Environment.CurrentDirectory;

    /// <summary>默认库文件(原版固定为 <c>&lt;当前目录&gt;/KM2.dat</c>)。</summary>
    public static string DatabasePath => Path.Combine(DataDirectory, AppConstants.DatabaseFileName);

    /// <summary>默认备份目录。</summary>
    public static string BackupsDirectory => Path.Combine(DataDirectory, AppConstants.BackupsPathName);
}
