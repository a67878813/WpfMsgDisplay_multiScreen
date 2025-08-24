using System.Configuration;
using System.Data;
using System.Windows;

//console
using System.Runtime.InteropServices;


namespace WpfMsgDisplay
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();



        protected override void OnStartup(StartupEventArgs e)
        {
            // 在应用程序启动时分配一个控制台
            AllocConsole();

            // 可选：打印启动参数
            if (e.Args.Length > 0)
            {
                Console.WriteLine("--- 应用程序启动参数 ---");
                foreach (var arg in e.Args)
                {
                    Console.WriteLine(arg);
                }
            }

            base.OnStartup(e);
        }
    }

}
