using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static System.Windows.Forms.Design.AxImporter;
using static WpfMsgDisplay.MainWindow;


namespace WpfMsgDisplay
{



    /// <summary>
    /// NotificationWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NotificationWindow : Window
    {
   
        // 引用外部共享的队列
        private readonly ConcurrentQueue<string> _messageQueue;
        private readonly DispatcherTimer _displayTimer;
        private const int MessageDisplayDurationSeconds = 3;
        private const int MessageFastDisplayDurationSeconds = 1;
        //private readonly int _currentScreenIndex;
        private readonly IQueueUpdater _updater;
        // 声明 Storyboard 变量
        private Storyboard _fadeInStoryboard;
        private Storyboard _fadeOutStoryboard;
        private Storyboard _flashStoryboard;
        private bool _isFirstMessage = true;

        // 添加一个新参数来判断是否快速显示

        public NotificationWindow()
        {
            InitializeComponent();
            MessageText1.Text = "默认构造字符串";
        }




        // 添加
        public NotificationWindow(ConcurrentQueue<string> queue, IQueueUpdater updater)
        {
            InitializeComponent();
            _messageQueue = queue;
            _updater = updater;
            // 查找并获取 Storyboard 资源
            _fadeInStoryboard = (Storyboard)this.Resources["FadeInStoryboard"];
            _fadeOutStoryboard = (Storyboard)this.Resources["FadeOutStoryboard"];
            _flashStoryboard = (Storyboard)this.Resources["FlashStoryboard"];
            // 为 FadeOut 动画添加完成事件处理器，以便在动画结束后关闭窗口
            _fadeOutStoryboard.Completed += (sender, e) => this.Close();
            // 为_flashStoryboard闪烁动画添加完成事件，以便结束后重启计时器
            _flashStoryboard.Completed += (sender, e) => _displayTimer.Start();

            _displayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(MessageDisplayDurationSeconds)
            };
            _displayTimer.Tick += DisplayNextMessage;




            // 立即开始处理，如果队列非空
            if (!_messageQueue.IsEmpty)
            {
                _displayTimer.Start();
                DisplayNextMessage(null, null); // 立即显示第一条
            }


        }
        



        public void UpdateMessages()
        {
            // 如果计时器未运行，立即开始处理
            if (!_displayTimer.IsEnabled && !_messageQueue.IsEmpty)
            {
                _displayTimer.Start();
                DisplayNextMessage(null, null);
            }


        }

        // 新增一个私有方法来处理消息解析和UI更新
        private void ProcessAndDisplayMessage(string jsonString)
        {
            Console.WriteLine(jsonString);
            try
            {

                MyData myData = JsonSerializer.Deserialize<MyData>(jsonString);

                MessageText1.Text = myData.message1;
                MessageText2.Text = myData.message2;
                MessageText3.Text = myData.message3;

                // 更新主窗口队列数量
                //_updater.UpdateQueueCount(_messageQueue.Count);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON 解析失败: {ex.Message}");
                Console.WriteLine($"原始字符串: {jsonString}");
            }
        }

        /// <summary>
        /// 计时器触发时调用，用于显示队列中的下一条消息。
        /// </summary>
        private void DisplayNextMessage(object sender, EventArgs e)
        {
            _updater.UpdateQueueCount(_messageQueue.Count);
            // 从共享队列中出队
            if (_messageQueue.TryDequeue(out string jsonString))
            {

                    ProcessAndDisplayMessage(jsonString);
                    //
                    if (!_isFirstMessage)
                        {
                            // 停止主计时器，以便控制消息显示间隔
                            _displayTimer.Stop();
                           _flashStoryboard.Begin(this);
                             // 动画完成后，_flashStoryboard.Completed 事件会重新启动计时器

                        }
                    else // 首次播放消息
                        {
                            // 确保初始不透明度为 0
                            this.Opacity = 0.3;
                            _fadeInStoryboard.Begin();
                            _isFirstMessage = false;

                            // 淡入动画完成后启动主计时器
                            _fadeInStoryboard.Completed += (s, args) => _displayTimer.Start();
                        }

                    // 重新开始计时，确保每条消息显示指定时长
                    _displayTimer.Stop();
                    _displayTimer.Start();
             }

            


            else
            {
                // 队列为空，停止计时器

                _displayTimer.Stop();
                _fadeOutStoryboard.Begin();
                // 窗口将在动画完成后通过 _fadeOutStoryboard.Completed 事件关闭

            }
        }
    }
}
