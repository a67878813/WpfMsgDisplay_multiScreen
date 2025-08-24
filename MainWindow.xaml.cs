using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

//11111111111
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using static WpfMsgDisplay.MainWindow;


//screen
//using System.Windows.Forms; // 确保已添加此引用
using Screen = System.Windows.Forms.Screen;
//using System.Runtime.InteropServices;
using System.Windows.Interop;
using WpfApplication = System.Windows.Application;
public static class ScreenExtensions
{
    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    public static IntPtr GetHmonitor(this System.Windows.Forms.Screen screen)
    {
        return MonitorFromWindow(screen.DeviceName.GetHashCode(), 2); // MONITOR_DEFAULTTONEAREST
    }
}



namespace WpfMsgDisplay
{

    // 1. 定义一个接口
    public interface IQueueUpdater
    {
        void UpdateQueueCount(int count);
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IQueueUpdater
    {
        private const int Port = 18880;
        private TcpListener listener;
        private bool isListening = false;

        // 新增：用于保存当前屏幕索引的静态变量
        public static int CurrentScreenIndex { get; private set; } = 0;
        // 用于反序列化的类，需要与JSON结构匹配
        public class MyData
        {
            //准备 JSON 数据
            //data = {"message": "Hello from Python!"}
            public string message1 { get; set; } = "nul";
            public string message2 { get; set; } = "nul";
            public string message3 { get; set; } = "nul";
        }

        //线程安全的队列
        public static ConcurrentQueue<string> messageQueue = new ConcurrentQueue<string>();

        // 使用信号量来通知消费者任务有新数据
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);

        // 在 MainWindow 类中添加一个私有变量
        private NotificationWindow _notificationWindow;



        // P/Invoke for GetDpiForMonitor
        [DllImport("Shcore.dll")]
        static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_Dpi_Type dpiType, out uint dpiX, out uint dpiY);

        enum Monitor_Dpi_Type
        {
            MDT_Effective_Dpi = 0,
            MDT_Angular_Dpi = 1,
            MDT_Raw_Dpi = 2,
        }

        /// <summary>
        /// 获取指定屏幕的 DPI
        /// </summary>
        public static double GetScreenDpi(Screen screen)
        {
            IntPtr hmonitor = new WindowInteropHelper(WpfApplication.Current.MainWindow).Handle;

            // 尝试从当前主窗口获取屏幕句柄，然后获取 DPI
            hmonitor = Screen.FromHandle(hmonitor).GetHmonitor();
            uint dpiX = 0, dpiY = 0;

            try
            {
                GetDpiForMonitor(hmonitor, Monitor_Dpi_Type.MDT_Effective_Dpi, out dpiX, out dpiY);
                return dpiX / 96.0; // 96是WPF默认的DPI
            }
            catch
            {
                // 失败时返回默认值
                return 1.0;
            }
        }

        // 新增：用于在UI线程上更新队列数量的公共方法
        public void UpdateQueueCount(int count)
        {
            // 直接更新UI元素，因为该方法会在UI线程上被调用
            QueueCountDisplay.Text = $"队列数量: {count}";
        }





        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 在这里启动消费者任务
            // 不需要 await，因为它是一个长时间运行的后台任务
            Task.Run(() => StartConsumerTask());

            // 初始时设置一次屏幕索引
            UpdateScreenIndex();
        }
        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += Window_Loaded;
            // 新增：订阅 LocationChanged 事件
            this.LocationChanged += MainWindow_LocationChanged;

        }

        /// <summary>
        /// 新增：当窗口位置改变时，更新屏幕索引
        /// </summary>
        private void MainWindow_LocationChanged(object sender, System.EventArgs e)
        {
            UpdateScreenIndex();
        }

        /// <summary>
        /// 新增：获取并更新当前窗口所在的屏幕索引
        /// </summary>
        private void UpdateScreenIndex()
        {
            // 获取当前窗口的句柄
            var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            // 根据句柄获取窗口所在的屏幕
            Screen currentScreen = Screen.FromHandle(windowHandle);

            // 查找该屏幕在所有屏幕列表中的索引
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                if (Screen.AllScreens[i].Equals(currentScreen))
                {
                    CurrentScreenIndex = i;
                    // 可选：在UI上显示当前屏幕索引，方便调试
                    Dispatcher.Invoke(() => ScreenIndexDisplay.Text = $"当前屏幕索引: {CurrentScreenIndex}");

                    if (Screen.AllScreens[i].Equals(currentScreen))
                    {
                        CurrentScreenIndex = i;
                        // 新增：打印当前屏幕的索引和参数
                        //Console.WriteLine($"--- 屏幕信息更新 ---");
                        Console.WriteLine($"当前屏幕索引: {CurrentScreenIndex}");
                        Console.WriteLine($"屏幕名称: {currentScreen.DeviceName}");
                        Console.WriteLine($"屏幕工作区域: {currentScreen.WorkingArea}");
                        Console.WriteLine($"屏幕总区域: {currentScreen.Bounds}");
                        //Console.WriteLine($"是否为主屏幕: {currentScreen.Primary}");
                        break;
                    }
                    break;
                }
            }
        }

        private void ShowNotificationButton_Click(object sender, RoutedEventArgs e)
        {

            var  myobj = new MyData
            {
                message1="ceshi操作成功111111",
                message2 = "文1111111222222222。",
                message3 = "提示：下次操333333333。"
            };
            string jsonString = JsonSerializer.Serialize(myobj);

            messageQueue.Enqueue(jsonString);
            Dispatcher.Invoke(() =>
            {
                UpdateQueueCount(messageQueue.Count);
            });
            // 成功入队后，释放一个信号，唤醒消费者任务
            _semaphore.Release();
            // 切换到 UI 线程处理数据
            //Dispatcher.Invoke(() =>

            //{


            //    // 如果窗口不存在，则创建并显示它
            //    if (_notificationWindow == null || !_notificationWindow.IsLoaded)
            //    {
            //        _notificationWindow = new NotificationWindow(messages, false); // 首次创建
            //        _notificationWindow.Show();

            //        // 订阅 ContentRendered 事件来设置初始位置
            //        _notificationWindow.ContentRendered += (sender, args) =>
            //        {
            //            var notification = sender as NotificationWindow;
            //            if (notification != null)
            //            {
            //                Console.WriteLine($"初次创建窗口");
            //                Screen targetScreen = Screen.AllScreens[CurrentScreenIndex];
            //                // 获取目标屏幕的DPI缩放比例
            //                double dpiScale = GetScreenDpi(targetScreen);
            //                notification.LayoutTransform = new ScaleTransform(dpiScale, dpiScale);
            //                // 强制窗口进行布局计算以获取正确的尺寸
            //                notification.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            //                notification.Arrange(new Rect(notification.DesiredSize));

            //                // 根据目标屏幕的工作区域设置弹窗的初始位置
            //                double screenRight = targetScreen.WorkingArea.Right;
            //                double screenBottom = targetScreen.WorkingArea.Bottom;

            //                notification.Left = screenRight - notification.ActualWidth - 1;
            //                notification.Top = screenBottom - notification.ActualHeight - 1;
            //            }
            //        };

            //    }

            //    else
            //    {
            //        // 如果窗口已存在，则只更新内容并重置动画
            //        _notificationWindow.UpdateMessages();
            //        Console.WriteLine($"2  更新窗口");
            //        // 重新定位窗口
            //        Screen targetScreen = Screen.AllScreens[CurrentScreenIndex];
            //        double screenRight = targetScreen.WorkingArea.Right;
            //        double screenBottom = targetScreen.WorkingArea.Bottom;

            //        _notificationWindow.Left = screenRight - _notificationWindow.ActualWidth - 1;
            //        _notificationWindow.Top = screenBottom - _notificationWindow.ActualHeight - 1;
            //    }



            //});
        }


        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (isListening) return;

            isListening = true;
            MessageDisplay.Text = "Listening for connections on port 18880...";
            await StartListeningAsync();

        }

        private async Task StartListeningAsync()
        {
            try
            {
                // 创建 TcpListener 实例，监听所有网络接口的指定端口
                listener = new TcpListener(IPAddress.Any, Port);
                listener.Start();
                while (isListening)
                {
                    // 异步等待客户端连接
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    // 异步处理客户端通信，不阻塞主循环
                    _ = HandleClientAsync(client);

                }
            }
            catch (Exception ex)
            {
                // UI 线程安全更新
                Dispatcher.Invoke(() => MessageDisplay.Text = $"Error: {ex.Message}");
            }

        }



        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[client.ReceiveBufferSize];
                    int bytesRead = await stream.ReadAsync(buffer, offset: 0, buffer.Length);
                    // 将字节数组转换为字符串
                    string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // 在后台线程中进行数据处理
                    // 检查是否是有效的JSON
                    if (receivedData.StartsWith("{") && receivedData.EndsWith("}"))
                    {
                        
                        messageQueue.Enqueue(receivedData);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateQueueCount(messageQueue.Count);
                        });
                        // 成功入队后，释放一个信号，唤醒消费者任务
                        _semaphore.Release();

                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageDisplay.Text += $"\nReceived non-JSON data: {receivedData}";
                        });
                    }

                }



            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageDisplay.Text += $"\nClient communication error: {ex.Message}");
            }
            finally
            {
                client.Close();
            }

        }



        // 3. 消费者任务，在应用启动时调用一次
        private async Task StartConsumerTask()
        {
            while (true)
            {
                // 等待信号，直到有新数据到达。
                // 这是一个高效的阻塞操作，不会占用CPU。
                await _semaphore.WaitAsync();
                // 只要队列中有数据，就持续处理
                while (!messageQueue.IsEmpty)
                {

                    //List<string> messages = new List<string> { myData.message1, myData.message2, myData.message3 };
                    // 切换到 UI 线程处理数据
                    Dispatcher.Invoke(() =>
                    {
                        // 如果窗口不存在，则创建并显示它
                        if (_notificationWindow == null || !_notificationWindow.IsLoaded)
                        {
                            _notificationWindow = new NotificationWindow(messageQueue, this); // 首次创建
                            _notificationWindow.Show();
                            // 订阅 ContentRendered 事件来设置初始位置
                            _notificationWindow.ContentRendered += (sender, args) =>
                            {
                                var notification = sender as NotificationWindow;
                                if (notification != null)
                                {
                                    Console.WriteLine($"初次创建窗口");
                                    Screen targetScreen = Screen.AllScreens[CurrentScreenIndex];
                                    // 获取目标屏幕的DPI缩放比例
                                    double dpiScale = GetScreenDpi(targetScreen);
                                    notification.LayoutTransform = new ScaleTransform(dpiScale, dpiScale);
                                    // 强制窗口进行布局计算以获取正确的尺寸
                                    notification.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                    notification.Arrange(new Rect(notification.DesiredSize));
                                    // 根据目标屏幕的工作区域设置弹窗的初始位置
                                    double screenRight = targetScreen.WorkingArea.Right;
                                    double screenBottom = targetScreen.WorkingArea.Bottom;
                                    notification.Left = screenRight - notification.ActualWidth - 1;
                                    notification.Top = screenBottom - notification.ActualHeight - 1;
                                }
                            };
                        }

                        else
                        {
                            // 如果窗口已存在，内部处理
                            _notificationWindow.UpdateMessages();
                        }



                    });
                }

            }
        }



    }

}