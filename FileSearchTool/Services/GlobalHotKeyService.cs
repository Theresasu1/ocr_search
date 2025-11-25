using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 全局快捷键服务
    /// </summary>
    public class GlobalHotKeyService : IDisposable
    {
        private readonly Dictionary<int, HotKey> _hotKeys = new Dictionary<int, HotKey>();
        private readonly IntPtr _hWnd;
        

        public GlobalHotKeyService(Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hWnd = helper.EnsureHandle();
            // 注册窗口消息处理
            HwndSource.FromHwnd(_hWnd)?.AddHook(WndProc);
        }

        public void RegisterHotKey(Key key, System.Windows.Input.ModifierKeys modifiers, Action action, string description)
        {
            var hotKey = new HotKey
            {
                Key = key,
                Modifiers = modifiers,
                Action = action,
                Description = description
            };

            var id = hotKey.GetHashCode();
            
            // 先注销旧的，避免冲突
            if (_hotKeys.ContainsKey(id))
            {
                UnregisterHotKey(_hWnd, id);
                _hotKeys.Remove(id);
            }
            
            // 注册系统级热键
            var virtualKey = KeyInterop.VirtualKeyFromKey(key);
            uint fsModifiers = ConvertModifiers(modifiers);
            
            if (RegisterHotKey(_hWnd, id, fsModifiers, virtualKey))
            {
                _hotKeys.Add(id, hotKey);
            }
            else
            {
                var error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"注册全局快捷键失败，错误代码: {error}. 该快捷键可能已被其他程序占用。");
            }
        }

        public void UnregisterHotKey(Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            var dummy = new HotKey { Key = key, Modifiers = modifiers };
            var id = dummy.GetHashCode();
            if (_hotKeys.ContainsKey(id))
            {
                UnregisterHotKey(_hWnd, id);
                _hotKeys.Remove(id);
            }
        }
        
        /// <summary>
        /// 清空所有热键
        /// </summary>
        public void UnregisterAllHotKeys()
        {
            var ids = _hotKeys.Keys.ToList();
            foreach (var id in ids)
            {
                UnregisterHotKey(_hWnd, id);
            }
            _hotKeys.Clear();
        }
        
        /// <summary>
        /// 转换ModifierKeys为uint类型
        /// </summary>
        private uint ConvertModifiers(System.Windows.Input.ModifierKeys modifiers)
        {
            uint result = 0;
            if ((modifiers & System.Windows.Input.ModifierKeys.Alt) == System.Windows.Input.ModifierKeys.Alt)
                result |= MOD_ALT;
            if ((modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                result |= MOD_CONTROL;
            if ((modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
                result |= MOD_SHIFT;
            if ((modifiers & System.Windows.Input.ModifierKeys.Windows) == System.Windows.Input.ModifierKeys.Windows)
                result |= MOD_WIN;
            return result;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (_hotKeys.ContainsKey(id))
                {
                    _hotKeys[id].Action?.Invoke();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            // 注销所有热键
            foreach (var id in _hotKeys.Keys)
            {
                UnregisterHotKey(_hWnd, id);
            }
            // 移除窗口消息处理
            HwndSource.FromHwnd(_hWnd)?.RemoveHook(WndProc);
        }

        #region Win32 API 导入

        private const int WM_HOTKEY = 0x0312;
        
        // ModifierKeys常量
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        private class HotKey
        {
            public Key Key { get; set; }
            public System.Windows.Input.ModifierKeys Modifiers { get; set; }
            public Action Action { get; set; } = () => { };
            public string Description { get; set; } = string.Empty;
            
            public override int GetHashCode()
            {
                return HashCode.Combine(Key, Modifiers);
            }
            
            public override bool Equals(object? obj)
            {
                if (obj is HotKey other)
                {
                    return Key == other.Key && Modifiers == other.Modifiers;
                }
                return false;
            }
        }
    }
}
