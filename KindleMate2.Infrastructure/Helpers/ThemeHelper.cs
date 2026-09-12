using Microsoft.Win32;

namespace KindleMate2.Infrastructure.Helpers {
    public static class ThemeHelper {
        /// <summary>
        /// 检测操作系统是否为深色(应用)主题。
        ///
        /// Windows:直接读取与 DarkModeCS.GetWindowsColorMode() 相同的注册表键,
        /// 使类库层不再依赖 DarkModeForms(WinForms 子模块),从而可被非 WinForms 的 UI 壳复用。
        /// 语义与原实现一致:AppsUseLightTheme &lt;= 0 视为深色;读取失败按浅色处理。
        ///
        /// 非 Windows:该注册表键不存在,统一返回 false(浅色)。各平台 UI 壳应自行决定
        /// 默认主题(Avalonia 壳走 ThemeVariant.Default 跟随系统,不依赖此方法)。
        /// </summary>
        public static bool IsWindowsDarkTheme() {
            if (!OperatingSystem.IsWindows()) {
                return false;
            }

            try {
                var mode = Registry.GetValue(
                    @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", -1);
                return mode is int appsUseLightTheme && appsUseLightTheme <= 0;
            } catch {
                return false;
            }
        }
    }
}
