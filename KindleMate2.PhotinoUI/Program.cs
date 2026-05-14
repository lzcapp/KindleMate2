using PhotinoNET;
using System;
using System.Text.Json;

namespace KindleMate2.PhotinoUI
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var window = new PhotinoWindow()
                .SetTitle("Kindle Mate 2 (Mac)")
                .SetUseOsDefaultSize(false)
                .SetSize(1024, 768)
                .Center()
                .Load("wwwroot/index.html"); // 加载你 Vite 打包过来的 Vue 界面

            // 注册前后端通信通道
            window.RegisterWebMessageReceivedHandler((object sender, string message) =>
            {
                var photinoWindow = (PhotinoWindow)sender;
                Console.WriteLine($"收到前端消息: {message}");
                // 你的业务逻辑分发会写在这里
            });

            window.WaitForClose();
        }
    }
}