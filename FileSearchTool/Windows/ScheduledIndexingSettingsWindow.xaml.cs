using System;
using System.Windows;
using System.Windows.Controls;
using FileSearchTool.Services;
using WPFMessageBox = System.Windows.MessageBox;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace FileSearchTool.Windows
{
    /// <summary>
    /// 定时索引设置窗口
    /// </summary>
    public partial class ScheduledIndexingSettingsWindow : Window
    {
        private readonly ScheduledIndexingService _scheduledIndexingService;
        private string _indexPath;
        private int _selectedInterval = 60; // 默认1小时
        private bool _isCustomInterval = false;
        private const string SettingsConfigFile = "settings.config";

        public ScheduledIndexingSettingsWindow(ScheduledIndexingService scheduledIndexingService, string indexPath)
        {
            InitializeComponent();
            _scheduledIndexingService = scheduledIndexingService;
            _indexPath = indexPath;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 初始化时间间隔选项
            IntervalComboBox.Items.Add(new ComboBoxItem { Content = "1小时", Tag = 60 });
            IntervalComboBox.Items.Add(new ComboBoxItem { Content = "3小时", Tag = 180 });
            IntervalComboBox.Items.Add(new ComboBoxItem { Content = "5小时", Tag = 300 });
            IntervalComboBox.Items.Add(new ComboBoxItem { Content = "自定义...", Tag = -1 });
            
            // 默认选择1小时
            IntervalComboBox.SelectedIndex = 0;
            CustomIntervalTextBox.IsEnabled = false;
            
            // 设置当前选中状态的标记
            UpdateSelectionMarker();
        }

        private void UpdateSelectionMarker()
        {
            // 更新所有选项的显示，添加原点标记表示当前选中状态
            for (int i = 0; i < IntervalComboBox.Items.Count; i++)
            {
                if (IntervalComboBox.Items[i] is ComboBoxItem item)
                {
                    string content = item.Content?.ToString() ?? "";
                    // 移除现有的标记
                    if (content.StartsWith("● ") || content.StartsWith("○ "))
                    {
                        content = content.Substring(2);
                    }
                    
                    // 添加适当的标记
                    if (i == IntervalComboBox.SelectedIndex)
                    {
                        item.Content = "● " + content; // 选中的项目用实心圆标记
                    }
                    else
                    {
                        item.Content = "○ " + content; // 未选中的项目用空心圆标记
                    }
                }
            }
            
            // 强制刷新ComboBox的显示
            IntervalComboBox.Items.Refresh();
        }

        private void IntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntervalComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                int interval = (int)selectedItem.Tag;
                
                if (interval == -1)
                {
                    // 选择自定义选项
                    _isCustomInterval = true;
                    CustomIntervalTextBox.IsEnabled = true;
                    CustomIntervalTextBox.Focus();
                }
                else
                {
                    // 选择预设选项
                    _isCustomInterval = false;
                    CustomIntervalTextBox.IsEnabled = false;
                    _selectedInterval = interval;
                }
                
                // 更新选中标记
                UpdateSelectionMarker();
            }
        }

        private void CustomIntervalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(CustomIntervalTextBox.Text, out int interval) && interval > 0)
            {
                _selectedInterval = interval;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 验证自定义间隔输入
                if (_isCustomInterval)
                {
                    if (!int.TryParse(CustomIntervalTextBox.Text, out int interval) || interval <= 0)
                    {
                        WPFMessageBox.Show("请输入有效的间隔时间（分钟）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    _selectedInterval = interval;
                }

                // 重新启动定时任务以应用新的时间配置
                _scheduledIndexingService?.StopScheduledIndexing();
                _scheduledIndexingService?.StartScheduledIndexing(_selectedInterval, _indexPath);

                WPFMessageBox.Show($"定时索引已设置为每{_selectedInterval}分钟执行一次", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
                
                this.Close();
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"设置定时索引失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}