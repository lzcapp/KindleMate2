using System.IO;
using KindleMate2.Application;
using KindleMate2.Application.Services;
using KindleMate2.Devices.Windows;
using KindleMate2.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using static System.Windows.Forms.Application;

namespace KindleMate2 {
    internal static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main() {
            EnableVisualStyles();
            SetCompatibleTextRenderingDefault(false);

            var services = new ServiceCollection();
            services.AddKindleMateServices();
            // Windows 壳注册平台专有的设备实现(Application 层不再引用 Windows 专有程序集)
            services.AddSingleton<IDeviceManager>(_ => new DeviceManager(
                Path.Combine(AppConstants.SystemPathName, AppConstants.VersionFileName)));
            services.AddTransient<FrmMain>(); // Form should be transient (created per request)

            using var serviceProvider = services.BuildServiceProvider();
            var mainForm = serviceProvider.GetRequiredService<FrmMain>();
            Run(mainForm);
        }
    }
}
