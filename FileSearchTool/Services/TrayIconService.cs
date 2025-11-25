using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows;
using WindowsFormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using WindowsFormsContextMenu = System.Windows.Forms.ContextMenuStrip;
using WindowsFormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using WindowsFormsToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using WindowsFormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;
using WindowsFormsMouseButtons = System.Windows.Forms.MouseButtons;
using WindowsFormsIcon = System.Drawing.Icon;
using WindowsFormsSystemIcons = System.Drawing.SystemIcons;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 系统托盘图标服务
    /// </summary>
    public class TrayIconService : IDisposable
    {
        private WindowsFormsNotifyIcon? _notifyIcon;
        private Window? _mainWindow;
        private bool _isDisposed = false;

        public TrayIconService(Window mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            if (_isDisposed) return;

            // 创建NotifyIcon
            _notifyIcon = new WindowsFormsNotifyIcon
            {
                Icon = WindowsFormsIcon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? WindowsFormsSystemIcons.Application,
                Visible = true,
                Text = "文件内容索引与搜索工具"
            };

            // 创建上下文菜单
            var contextMenu = new WindowsFormsContextMenu();

            // 显示/隐藏窗口菜单项
            var showHideMenuItem = new WindowsFormsToolStripMenuItem("显示/隐藏");
            showHideMenuItem.Click += ShowHideMenuItem_Click;
            contextMenu.Items.Add(showHideMenuItem);

            // 分隔符
            contextMenu.Items.Add(new WindowsFormsToolStripSeparator());

            // 退出菜单项
            var exitMenuItem = new WindowsFormsToolStripMenuItem("退出");
            exitMenuItem.Click += ExitMenuItem_Click;
            contextMenu.Items.Add(exitMenuItem);

            // 设置NotifyIcon的上下文菜单
            _notifyIcon.ContextMenuStrip = contextMenu;

            // 设置NotifyIcon点击事件
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
        }

        private void ShowHideMenuItem_Click(object? sender, EventArgs e)
        {
            ToggleMainWindowVisibility();
        }

        private void ExitMenuItem_Click(object? sender, EventArgs e)
        {
            ExitApplication();
        }

        private void NotifyIcon_MouseClick(object? sender, WindowsFormsMouseEventArgs e)
        {
            // 左键单击托盘图标时切换窗口显示状态
            if (e.Button == WindowsFormsMouseButtons.Left)
            {
                ToggleMainWindowVisibility();
            }
        }

        private void ToggleMainWindowVisibility()
        {
            if (_mainWindow == null || _isDisposed) return;

            if (_mainWindow.Visibility == Visibility.Visible)
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        private void ExitApplication()
        {
            if (_isDisposed) return;

            _mainWindow?.Close();
            System.Windows.Application.Current?.Shutdown();
        }

        public void ShowBalloonTip(string title, string message, int timeout = 3000)
        {
            if (_isDisposed || _notifyIcon == null) return;

            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.ShowBalloonTip(timeout);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                
                // 清理托盘图标
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
                
                _mainWindow = null;
            }
        }
    }
}