using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FileSearchTool.Services;
using WPFMessageBox = System.Windows.MessageBox;
using WPFUserControl = System.Windows.Controls.UserControl;
using WPFApplication = System.Windows.Application; // 明确使用WPF的Application

namespace FileSearchTool.Windows
{
    public partial class HotKeySettingsControl : WPFUserControl
    {
        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;
        private const string HotKeyConfigFile = "hotkey.config";
        private readonly GlobalHotKeyService _globalHotKeyService;
        private readonly Action _toggleWindowAction;
        private readonly Action<string> _updateStatus;
        private NotificationService? _notificationService; // 添加可空引用修饰符

        public HotKeySettingsControl(GlobalHotKeyService globalHotKeyService, Action toggleWindowAction, Action<string> updateStatus)
        {
            InitializeComponent();
            _globalHotKeyService = globalHotKeyService;
            _toggleWindowAction = toggleWindowAction;
            _updateStatus = updateStatus;
            
            // 尝试获取主窗口以初始化通知服务
            var mainWindow = WPFApplication.Current?.MainWindow; // 使用WPF的Application
            if (mainWindow != null)
            {
                _notificationService = new NotificationService(mainWindow);
            }
            
            LoadCurrentHotKey();
        }

        private void LoadCurrentHotKey()
        {
            try
            {
                if (File.Exists(HotKeyConfigFile))
                {
                    string[] lines = File.ReadAllLines(HotKeyConfigFile);
                    if (lines.Length >= 2)
                    {
                        if (Enum.TryParse<ModifierKeys>(lines[0], out var modifiers) &&
                            Enum.TryParse<Key>(lines[1], out var key))
                        {
                            _currentKey = key;
                            _currentModifiers = modifiers;
                            UpdateCurrentHotKeyText();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 使用通知服务显示错误信息
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"加载快捷键配置失败: {ex.Message}");
                }
                else
                {
                    WPFMessageBox.Show($"加载快捷键配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateCurrentHotKeyText()
        {
            string modifiersText = GetModifiersText(_currentModifiers);
            string keyText = _currentKey == Key.None ? "" : _currentKey.ToString();
            CurrentHotKeyText.Text = string.IsNullOrEmpty(modifiersText) && string.IsNullOrEmpty(keyText) 
                ? "未设置" 
                : $"{modifiersText}{keyText}";
        }

        private string GetModifiersText(ModifierKeys modifiers)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl+");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift+");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt+");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win+");
            return string.Join("", parts);
        }

        private void HotKeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            
            // 获取修饰键状态
            var modifiers = Keyboard.Modifiers;
            
            // 忽略不适合作为快捷键的按键
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System || e.Key == Key.None)
            {
                return;
            }
            
            // 更新当前快捷键设置
            _currentKey = e.Key;
            _currentModifiers = modifiers;
            
            // 更新UI显示
            var modifiersText = GetModifiersText(_currentModifiers);
            var keyText = _currentKey.ToString();
            HotKeyTextBox.Text = $"{modifiersText}{keyText}";
            
            // 使用通知服务显示状态信息
            if (_notificationService != null)
            {
                _notificationService.ShowInfo("快捷键设置成功，点击确认保存更改");
            }
            else
            {
                // 备用方案：使用状态文本
                // 这里需要访问状态文本控件，但我们在用户控件中可能没有直接访问权限
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存到配置文件
                using (StreamWriter writer = new StreamWriter(HotKeyConfigFile))
                {
                    writer.WriteLine(_currentModifiers.ToString());
                    writer.WriteLine(_currentKey.ToString());
                }
                
                // 注册新的全局快捷键
                _globalHotKeyService.UnregisterAllHotKeys();
                if (_currentKey != Key.None)
                {
                    _globalHotKeyService.RegisterHotKey(_currentKey, _currentModifiers, _toggleWindowAction, "全局搜索快捷键");
                }
                
                // 使用通知服务显示成功信息
                if (_notificationService != null)
                {
                    _notificationService.ShowSuccess("快捷键设置已保存。");
                }
                else
                {
                    WPFMessageBox.Show("快捷键设置已保存。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                _updateStatus?.Invoke("快捷键设置已更新");
            }
            catch (Exception ex)
            {
                // 使用通知服务显示错误信息
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"设置快捷键失败: {ex.Message}");
                }
                else
                {
                    WPFMessageBox.Show($"设置快捷键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void HotKeyTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            HotKeyTextBox.SelectAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmButton_Click(sender, e);
        }

        private void ResetHotKeyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空当前快捷键设置
                _currentKey = Key.None;
                _currentModifiers = ModifierKeys.None;
                
                // 更新UI
                HotKeyTextBox.Text = "未设置";
                UpdateCurrentHotKeyText();
                
                // 使用通知服务显示状态信息
                if (_notificationService != null)
                {
                    _notificationService.ShowInfo("已重置为未设置。点击确认保存更改");
                }
                else
                {
                    // 备用方案：使用状态文本
                }
            }
            catch (Exception ex)
            {
                // 使用通知服务显示错误信息
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"重置快捷键失败: {ex.Message}");
                }
                else
                {
                    WPFMessageBox.Show($"重置快捷键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}