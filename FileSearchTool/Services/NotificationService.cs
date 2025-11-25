using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;  // 明确使用WPF的Color
using Brushes = System.Windows.Media.Brushes;  // 明确使用WPF的Brushes
using HorizontalAlignment = System.Windows.HorizontalAlignment;  // 明确使用WPF的HorizontalAlignment
using WPFMessageBox = System.Windows.MessageBox; // 明确使用WPF的MessageBox

namespace FileSearchTool.Services
{
    /// <summary>
    /// 统一消息通知服务
    /// </summary>
    public class NotificationService
    {
        private readonly Window _ownerWindow;
        private Border _notificationBorder;
        private TextBlock _messageTextBlock;
        private DispatcherTimer _hideTimer;

        public NotificationService(Window ownerWindow)
        {
            _ownerWindow = ownerWindow;
            InitializeNotificationPanel();
        }

        /// <summary>
        /// 初始化通知面板
        /// </summary>
        private void InitializeNotificationPanel()
        {
            // 确保在UI线程上执行
            if (!_ownerWindow.Dispatcher.CheckAccess())
            {
                _ownerWindow.Dispatcher.Invoke(InitializeNotificationPanel);
                return;
            }

            // 创建通知面板
            _notificationBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 50, 50, 50)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };

            // 创建消息文本块
            _messageTextBlock = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300
            };

            _notificationBorder.Child = _messageTextBlock;

            // 将通知面板添加到窗口
            if (_ownerWindow is System.Windows.Controls.UserControl || _ownerWindow is Window)
            {
                var grid = _ownerWindow.Content as Grid;
                if (grid == null)
                {
                    grid = new Grid();
                    var originalContent = _ownerWindow.Content;
                    _ownerWindow.Content = grid;
                    grid.Children.Add(new ContentPresenter { Content = originalContent });
                }
                grid.Children.Add(_notificationBorder);
            }

            // 初始化隐藏计时器
            _hideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _hideTimer.Tick += HideTimer_Tick;
        }

        private void HideTimer_Tick(object sender, EventArgs e)
        {
            _notificationBorder.Visibility = Visibility.Collapsed;
            _hideTimer.Stop();
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="durationSeconds">显示持续时间（秒）</param>
        private void ShowNotification(string message, int durationSeconds = 3)
        {
            if (!_ownerWindow.Dispatcher.CheckAccess())
            {
                _ownerWindow.Dispatcher.Invoke(() => ShowNotification(message, durationSeconds));
                return;
            }

            _messageTextBlock.Text = message;
            _notificationBorder.Visibility = Visibility.Visible;

            _hideTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        /// <summary>
        /// 显示成功消息
        /// </summary>
        public void ShowSuccess(string message, int durationSeconds = 3)
        {
            _notificationBorder.Background = new SolidColorBrush(Color.FromArgb(220, 50, 150, 50));
            ShowNotification(message, durationSeconds);
        }

        /// <summary>
        /// 显示警告消息
        /// </summary>
        public void ShowWarning(string message, int durationSeconds = 3)
        {
            _notificationBorder.Background = new SolidColorBrush(Color.FromArgb(220, 200, 150, 50));
            ShowNotification(message, durationSeconds);
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        public void ShowError(string message, int durationSeconds = 3)
        {
            _notificationBorder.Background = new SolidColorBrush(Color.FromArgb(220, 200, 50, 50));
            ShowNotification(message, durationSeconds);
        }

        /// <summary>
        /// 显示信息消息
        /// </summary>
        public void ShowInfo(string message, int durationSeconds = 3)
        {
            _notificationBorder.Background = new SolidColorBrush(Color.FromArgb(220, 50, 100, 200));
            ShowNotification(message, durationSeconds);
        }
    }
}