using Microsoft.Win32;

namespace KindleMate2.Infrastructure.Helpers {
    public static class ThemeHelper {
        /// <summary>
        /// 检测 Windows 是否为深色(应用)主题。直接读取与 DarkModeCS.GetWindowsColorMode()
        /// 相同的注册表键,使类库层不再依赖 DarkModeForms(WinForms 子模块),
        /// 从而可被非 WinForms 的 UI 壳(如 Avalonia)复用。
        /// 语义与原实现一致:AppsUseLightTheme &lt;= 0 视为深色;读取失败按浅色处理。
        /// </summary>
        public static bool IsWindowsDarkTheme() {
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
