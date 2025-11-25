using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using FileSearchTool.Services;
using FileSearchTool;
using WPFCheckBox = System.Windows.Controls.CheckBox;

namespace FileSearchTool.Windows
{
    public partial class PreferencesWindow : Window
    {
        private readonly ScheduledIndexingService _scheduledIndexingService;
        private readonly IndexStorageService _indexStorage;
        private readonly Window _mainWindow;
        private readonly GlobalHotKeyService _globalHotKeyService;
        private readonly Action _toggleWindowAction;

        public PreferencesWindow(
            ScheduledIndexingService scheduledIndexingService, 
            IndexStorageService indexStorage,
            Window mainWindow,
            GlobalHotKeyService globalHotKeyService,
            Action toggleWindowAction)
        {
            InitializeComponent();
            _scheduledIndexingService = scheduledIndexingService;
            _indexStorage = indexStorage;
            _mainWindow = mainWindow;
            _globalHotKeyService = globalHotKeyService;
            _toggleWindowAction = toggleWindowAction;
            
            // 默认选中第一项
            CategoryListBox.SelectedIndex = 0;
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ListBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                LoadContent(tag);
            }
        }

        private void LoadContent(string? category)
        {
            ContentPanel.Children.Clear();

            if (category == null) return;

            switch (category)
            {
                case "Hotkey":
                    LoadHotkeyContent();
                    break;
                case "FileType":
                    LoadFileTypeContent();
                    break;
                case "Performance":
                    LoadPerformanceContent();
                    break;
                case "WindowBehavior":
                    LoadWindowBehaviorContent();
                    break;
            }
        }

        private void LoadHotkeyContent()
        {
            var content = new HotKeySettingsControl(_globalHotKeyService, _toggleWindowAction, msg =>
            {
                if (_mainWindow is MainWindow mw)
                {
                    mw.UpdateStatus(msg);
                }
            });
            ContentPanel.Children.Add(content);
        }

        private void LoadFileTypeContent()
        {
            var content = new FileTypeSettingsControl(_scheduledIndexingService);
            ContentPanel.Children.Add(content);
        }

        private void LoadPerformanceContent()
        {
            var content = new PerformanceSettingsControl(_scheduledIndexingService, msg =>
            {
                if (_mainWindow is MainWindow mw)
                {
                    mw.UpdateStatus(msg);
                }
            });
            ContentPanel.Children.Add(content);
        }

        private void LoadWindowBehaviorContent()
        {
            var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Vertical };

            var title = new TextBlock
            {
                Text = "窗口行为",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(title);

            var description = new TextBlock
            {
                Text = "设置关闭按钮行为：勾选后，点击关闭将最小化到任务栏；不勾选则直接关闭。",
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(description);

            var checkBox = new WPFCheckBox
            {
                Content = "关闭时最小化到任务栏",
                Margin = new Thickness(0, 10, 0, 10)
            };
            checkBox.IsChecked = LoadMinimizeOnCloseFlag();
            checkBox.Checked += (s, e) => { SaveMinimizeOnCloseFlag(true); ApplyMinimizePreference(true); };
            checkBox.Unchecked += (s, e) => { SaveMinimizeOnCloseFlag(false); ApplyMinimizePreference(false); };
            panel.Children.Add(checkBox);

            ContentPanel.Children.Add(panel);
        }

        private bool LoadMinimizeOnCloseFlag()
        {
            const string configFile = "performance.config";
            try
            {
                if (File.Exists(configFile))
                {
                    var lines = File.ReadAllLines(configFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MinimizeOnClose=", StringComparison.OrdinalIgnoreCase))
                        {
                            var value = line.Substring("MinimizeOnClose=".Length).Trim();
                            return bool.TryParse(value, out var flag) && flag;
                        }
                    }
                }
            }
            catch { }
            return false; // 默认直接关闭
        }

        private void SaveMinimizeOnCloseFlag(bool flag)
        {
            const string configFile = "performance.config";
            try
            {
                // 读入现有内容，更新或追加键值
                var lines = File.Exists(configFile) ? new System.Collections.Generic.List<string>(File.ReadAllLines(configFile)) : new System.Collections.Generic.List<string>();
                var found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith("IndexingCores="))
                        continue; // 保留其它键
                    if (lines[i].StartsWith("MinimizeOnClose="))
                    {
                        lines[i] = $"MinimizeOnClose={flag}";
                        found = true;
                    }
                }
                if (!found)
                {
                    lines.Add($"MinimizeOnClose={flag}");
                }
                File.WriteAllLines(configFile, lines);
            }
            catch { }
        }

        private void ApplyMinimizePreference(bool flag)
        {
            if (_mainWindow is MainWindow mw)
            {
                mw.SetMinimizeOnClose(flag);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
