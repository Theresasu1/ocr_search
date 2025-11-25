using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FileSearchTool.Services;
using WPFMessageBox = System.Windows.MessageBox;
using WPFUserControl = System.Windows.Controls.UserControl;

namespace FileSearchTool.Windows
{
    public partial class PerformanceSettingsControl : WPFUserControl
    {
        private readonly ScheduledIndexingService _scheduledIndexingService;
        private readonly Action<string> _updateStatus;
        private const string PerformanceConfigFile = "performance.config";
        private int _totalCores;

        public PerformanceSettingsControl(ScheduledIndexingService scheduledIndexingService, Action<string> updateStatus)
        {
            InitializeComponent();
            _scheduledIndexingService = scheduledIndexingService;
            _updateStatus = updateStatus;
            
            InitializeSettings();
        }

        private void InitializeSettings()
        {
            // 获取系统CPU核心数
            _totalCores = Environment.ProcessorCount;
            TotalCoresTextBlock.Text = _totalCores.ToString();
            
            // 设置滑块最大值为系统核心数
            CoresSlider.Maximum = _totalCores;
            
            // 加载保存的设置
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(PerformanceConfigFile))
                {
                    var lines = File.ReadAllLines(PerformanceConfigFile);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("IndexingCores="))
                        {
                            var value = line.Substring("IndexingCores=".Length);
                            if (int.TryParse(value, out int cores) && cores >= 1 && cores <= _totalCores)
                            {
                                CoresSlider.Value = cores;
                                return;
                            }
                        }
                    }
                }
                
                // 默认使用50%核心数
                CoresSlider.Value = Math.Max(1, _totalCores / 2);
            }
            catch
            {
                // 出错时使用默认值
                CoresSlider.Value = Math.Max(1, _totalCores / 2);
            }
        }

        private void CoresSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CoresValueTextBlock == null || PercentageTextBlock == null) return;
            
            int cores = (int)CoresSlider.Value;
            CoresValueTextBlock.Text = cores.ToString();
            
            // 计算百分比
            double percentage = (double)cores / _totalCores * 100;
            PercentageTextBlock.Text = $"{percentage:F0}%";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int cores = (int)CoresSlider.Value;
                
                // 保存到配置文件
                File.WriteAllText(PerformanceConfigFile, $"IndexingCores={cores}");
                
                // 应用到索引服务
                _scheduledIndexingService.SetIndexingCores(cores);
                
                // 同步状态栏
                _updateStatus?.Invoke($"索引使用核心数：{cores}（{(double)cores / _totalCores * 100:F0}%）");
                
                WPFMessageBox.Show(
                    $"性能设置已保存\n\n索引使用核心数：{cores}\n使用比例：{(double)cores / _totalCores * 100:F0}%",
                    "保存成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show(
                    $"保存设置失败: {ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
