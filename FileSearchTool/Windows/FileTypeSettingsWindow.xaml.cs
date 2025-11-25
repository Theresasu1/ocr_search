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
    /// 文件类型设置窗口
    /// </summary>
    public partial class FileTypeSettingsWindow : Window
    {
        private readonly ScheduledIndexingService _scheduledIndexingService;
        private const string SettingsConfigFile = "settings.config";
        private long _maxFileSizeBytes = 100 * 1024 * 1024; // 默认100MB
        private readonly string[] _defaultExtensions = new[]
        {
            // Office 文档（OpenXML）
            ".docx", ".docm", ".dotx", ".dotm",
            ".xlsx", ".xlsm", ".xltx", ".xltm", ".xlsb",
            ".pptx", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm",
            // 仅索引以下文本类型
            ".txt", ".log", ".md",
            // 同步允许 PDF 文件（与系统白名单一致）
            ".pdf"
        };

        public FileTypeSettingsWindow(ScheduledIndexingService scheduledIndexingService)
        {
            InitializeComponent();
            _scheduledIndexingService = scheduledIndexingService;
            InitializeUI();
        }

        private void InitializeUI()
        {
            // 初始化扩展名文本框
            ExtensionsTextBox.Text = string.Join(";", _defaultExtensions);
            IndexAllFilesCheckBox.IsChecked = false;

            // 如果调度服务已有配置接口，初始化 UI 以反映当前配置
            try
            {
                var currentExtensions = _scheduledIndexingService.GetAllowedExtensions();
                if (currentExtensions != null && currentExtensions.Any())
                {
                    ExtensionsTextBox.Text = string.Join(";", currentExtensions);
                }
                IndexAllFilesCheckBox.IsChecked = _scheduledIndexingService.GetIndexAllFiles();
            }
            catch { /* 忽略初始化读取异常 */ }
            
            // 读取配置文件中的最大文件大小与排除子目录
            LoadAdvancedSettingsFromConfig();
            MaxFileSizeTextBox.Text = Math.Max(1, (int)(_maxFileSizeBytes / (1024 * 1024))).ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 解析扩展名白名单
                var raw = ExtensionsTextBox.Text ?? string.Empty;
                var parts = raw.Split(new[] { ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var normalized = parts.Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.StartsWith(".") ? p : "." + p).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                // 索引范围开关
                bool indexAll = IndexAllFilesCheckBox.IsChecked == true;

                // 最大全文提取文件大小（MB -> bytes）
                long maxBytes;
                if (!string.IsNullOrWhiteSpace(MaxFileSizeTextBox.Text) && long.TryParse(MaxFileSizeTextBox.Text.Trim(), out long mb) && mb > 0)
                {
                    maxBytes = mb * 1024 * 1024;
                    _maxFileSizeBytes = maxBytes;
                }
                else
                {
                    WPFMessageBox.Show("请输入有效的大小（正整数MB）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 排除指定目录集合
                var excludedSubdirs = new List<string>();
                foreach (var item in ExcludedSubdirsListBox.Items)
                {
                    var s = item?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        excludedSubdirs.Add(s);
                    }
                }

                // 将设置应用到调度服务 -> IndexingService
                _scheduledIndexingService?.SetAllowedExtensions(normalized);
                _scheduledIndexingService?.SetIndexAllFiles(indexAll);
                _scheduledIndexingService?.SetMaxFileSize(maxBytes);
                _scheduledIndexingService?.SetExcludedSubdirectories(excludedSubdirs);
                
                // 保存到配置文件
                SaveIndexSettingsToConfig(normalized, indexAll, maxBytes, excludedSubdirs);

                WPFMessageBox.Show(
                    $"文件类型设置已保存\n\n" +
                    $"文件类型白名单：{string.Join(";", normalized)}\n" +
                    $"索引所有文件：{(indexAll ? "是" : "否")}\n" +
                    $"最大全文提取大小：{(maxBytes / (1024 * 1024))} MB\n" +
                    $"排除指定目录：{(excludedSubdirs.Count == 0 ? "(无)" : string.Join(";", excludedSubdirs))}", 
                    "设置成功", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Information);
                
                this.Close();
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveIndexSettingsToConfig(IEnumerable<string> extensions, bool indexAll, long maxFileSizeBytes, IEnumerable<string> excludedSubdirs)
        {
            try
            {
                using (var writer = new StreamWriter(SettingsConfigFile, false))
                {
                    writer.WriteLine($"IndexAllFiles={indexAll}");
                    writer.WriteLine($"AllowedExtensions={string.Join(";", extensions)}");
                    writer.WriteLine($"MaxFileSizeBytes={maxFileSizeBytes}");
                    writer.WriteLine($"ExcludedSubdirs={string.Join(";", excludedSubdirs ?? Array.Empty<string>())}");
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"保存索引设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAdvancedSettingsFromConfig()
        {
            try
            {
                if (File.Exists(SettingsConfigFile))
                {
                    var lines = File.ReadAllLines(SettingsConfigFile);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var idx = line.IndexOf('=');
                        if (idx <= 0) continue;
                        var key = line.Substring(0, idx).Trim();
                        var value = line.Substring(idx + 1).Trim();

                        if (string.Equals(key, "MaxFileSizeBytes", StringComparison.OrdinalIgnoreCase))
                        {
                            if (long.TryParse(value, out var bytes) && bytes > 0)
                            {
                                _maxFileSizeBytes = bytes;
                            }
                        }
                        else if (string.Equals(key, "ExcludedSubdirs", StringComparison.OrdinalIgnoreCase))
                        {
                            ExcludedSubdirsListBox.Items.Clear();
                            var parts = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var p in parts.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
                            {
                                ExcludedSubdirsListBox.Items.Add(p);
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略配置读取异常
            }
        }

        private void AddExcludedSubdirButton_Click(object sender, RoutedEventArgs e)
        {
            var raw = NewExcludedSubdirTextBox.Text ?? string.Empty;
            var parts = raw.Split(new[] { ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim())
                           .Where(s => !string.IsNullOrWhiteSpace(s))
                           .ToList();
            foreach (var p in parts)
            {
                // 避免重复
                bool exists = ExcludedSubdirsListBox.Items.Cast<object>().Any(x => string.Equals(x?.ToString(), p, StringComparison.OrdinalIgnoreCase));
                if (!exists)
                {
                    ExcludedSubdirsListBox.Items.Add(p);
                }
            }
            NewExcludedSubdirTextBox.Clear();
        }

        private void BrowseExcludedDirButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择要排除的目录",
                    ShowNewFolderButton = false
                };
                
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    // 检查是否已存在
                    bool exists = ExcludedSubdirsListBox.Items.Cast<object>().Any(x => string.Equals(x?.ToString(), dialog.SelectedPath, StringComparison.OrdinalIgnoreCase));
                    if (!exists)
                    {
                        ExcludedSubdirsListBox.Items.Add(dialog.SelectedPath);
                    }
                    else
                    {
                        WPFMessageBox.Show("该目录已在排除列表中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"浏览目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveExcludedSubdirButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = ExcludedSubdirsListBox.SelectedItems.Cast<object>().ToList();
            foreach (var item in selected)
            {
                ExcludedSubdirsListBox.Items.Remove(item);
            }
        }

        private void LoadDefaultButton_Click(object sender, RoutedEventArgs e)
        {
            ExtensionsTextBox.Text = string.Join(";", _defaultExtensions);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
