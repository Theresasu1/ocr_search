using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FileSearchTool.Services;
using WPFMessageBox = System.Windows.MessageBox;

namespace FileSearchTool.Windows
{
    /// <summary>
    /// HotKeySettingsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HotKeySettingsWindow : Window
    {
        private readonly Window _mainWindow;
        private Action _hotKeyAction;
        
        // 当前设置的快捷键
        private Key _currentKey = Key.None;
        private ModifierKeys _currentModifiers = ModifierKeys.None;
        
        // 配置文件路径
        private const string HotKeyConfigFile = "hotkey.config";
        
        public HotKeySettingsWindow(Window mainWindow, Action hotKeyAction)
        {
            InitializeComponent();
            
            _mainWindow = mainWindow;
            _hotKeyAction = hotKeyAction;
            
            // 加载当前设置的快捷键
            LoadCurrentHotKey();
        }
        
        private void LoadCurrentHotKey()
        {
            try
            {
                // 尝试从配置文件加载快捷键设置
                if (File.Exists(HotKeyConfigFile))
                {
                    string[] lines = File.ReadAllLines(HotKeyConfigFile);
                    if (lines.Length >= 2)
                    {
                        // 第一行是ModifierKeys
                        if (Enum.TryParse<ModifierKeys>(lines[0], out var modifiers))
                        {
                            _currentModifiers = modifiers;
                        }
                        
                        // 第二行是Key
                        if (Enum.TryParse<Key>(lines[1], out var key))
                        {
                            _currentKey = key;
                        }
                    }
                }
                
                UpdateCurrentHotKeyText();
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"加载快捷键设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateCurrentHotKeyText()
        {
            if (_currentKey == Key.None)
            {
                CurrentHotKeyText.Text = "未设置";
            }
            else
            {
                var modifiersText = GetModifiersText(_currentModifiers);
                var keyText = _currentKey.ToString();
                CurrentHotKeyText.Text = $"{modifiersText}{keyText}";
            }
        }
        
        private string GetModifiersText(ModifierKeys modifiers)
        {
            var result = string.Empty;
            if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                result += "Ctrl + ";
            if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                result += "Shift + ";
            if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                result += "Alt + ";
            if ((modifiers & ModifierKeys.Windows) == ModifierKeys.Windows)
                result += "Win + ";
            return result;
        }
        
        private void HotKeyTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            HotKeyTextBox.Text = "请按快捷键组合...";
            StatusText.Text = "按下想要设置的快捷键组合，如 Ctrl+Shift+F";
        }
        
        // 新增：捕获文本框上的键盘事件，优先于窗口级别，确保能够正确显示组合键
        private void HotKeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // 捕获用户按下的按键组合
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
            StatusText.Text = "快捷键设置成功，点击确认保存更改";
        }
        
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存设置到配置
                SaveHotKeySettings();
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"设置快捷键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        
        // 新增：重置快捷键按钮逻辑
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
                StatusText.Text = "已重置为未设置。点击确认保存更改";
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"重置快捷键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            // 兜底：在窗口级别捕获，当文本框获得焦点时也能工作
            if (HotKeyTextBox.IsFocused)
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
                StatusText.Text = "快捷键设置成功，点击确认保存更改";
            }
            
            base.OnKeyDown(e);
        }
        
        private void SaveHotKeySettings()
        {
            try
            {
                // 保存到配置文件
                using (StreamWriter writer = new StreamWriter(HotKeyConfigFile))
                {
                    writer.WriteLine(_currentModifiers.ToString());
                    writer.WriteLine(_currentKey.ToString());
                }
                
                StatusText.Text = "快捷键设置已保存，请重启程序使设置生效";
            }
            catch (Exception ex)
            {
                throw new Exception($"保存快捷键配置失败: {ex.Message}", ex);
            }
        }
        
        // 静态方法：从配置文件加载并注册快捷键
        public static void RegisterHotKeyFromConfig(Window window, Action hotKeyAction)
        {
            try
            {
                if (File.Exists(HotKeyConfigFile))
                {
                    string[] lines = File.ReadAllLines(HotKeyConfigFile);
                    if (lines.Length >= 2)
                    {
                        if (Enum.TryParse<ModifierKeys>(lines[0], out var modifiers) &&
                            Enum.TryParse<Key>(lines[1], out var key) &&
                            key != Key.None)
                        {
                            var hotKeyService = new GlobalHotKeyService(window);
                            hotKeyService.RegisterHotKey(key, modifiers, hotKeyAction, "全局搜索快捷键");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"注册全局快捷键失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}