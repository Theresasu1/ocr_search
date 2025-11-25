using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FileSearchTool.Data;
using FileSearchTool.Helpers;
using FileSearchTool.Model;
using FileSearchTool.Services;
using FileSearchTool.ViewModel;
using FileSearchTool.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using WPFMessageBox = System.Windows.MessageBox; // 明确指定WPF的MessageBox
using WindowsFormsApplication = System.Windows.Forms.Application;
using WindowsFormsMessageBox = System.Windows.Forms.MessageBox;
using WindowsFormsMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using WindowsFormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;
using WindowsFormsClipboard = System.Windows.Forms.Clipboard;
using System.Windows.Forms;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WPFTextBox = System.Windows.Controls.TextBox; // 明确指定WPF的TextBox
using WPFApplication = System.Windows.Application; // 明确指定WPF的Application

namespace FileSearchTool
{
    public partial class MainWindow : Window
    {
        private bool _minimizeOnClose;
        private DatabaseConfigService _configService;
        private SearchService _searchService;
        private IndexStorageService _indexStorageService;
        private IndexingService _indexingService;
        private NotificationService _notificationService;
        private SyntaxHighlightService _syntaxHighlightService;
        private MainViewModel _viewModel;
        private TrayIconService _trayIconService;
        private GlobalHotKeyService _hotKeyService;
        private ScheduledIndexingService _scheduledIndexingService;
        private DispatcherTimer _animationTimer;
        private List<(TextPointer start, TextPointer end)> _previewMatchRanges = new List<(TextPointer start, TextPointer end)>();
        private int _currentMatchIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化服务
            _configService = new DatabaseConfigService();
            var dbContext = new SearchDbContext(); // 使用无参构造函数
            var contentExtractor = new ContentExtractorService();
            var fileScanner = new FileScannerService();
            _searchService = new SearchService(dbContext);
            _indexStorageService = new IndexStorageService(dbContext, contentExtractor);
            _indexingService = new IndexingService(dbContext, contentExtractor, fileScanner);
            
            // 初始化通知服务
            _notificationService = new NotificationService(this);
            
            // 初始化语法高亮服务
            _syntaxHighlightService = new SyntaxHighlightService();
            
            // 初始化视图模型
            _viewModel = new MainViewModel(_notificationService)
            {
                SearchService = _searchService,
                IndexingService = _indexingService,
                IndexStorageService = _indexStorageService,
                ConfigService = _configService
            };
            
            DataContext = _viewModel;
            
            // 初始化其他服务
            _trayIconService = new TrayIconService(this);
            _hotKeyService = new GlobalHotKeyService(this);
            _scheduledIndexingService = new ScheduledIndexingService(_indexingService);
            
            // 初始化动画计时器
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _animationTimer.Tick += AnimationTimer_Tick;
            
            // 加载配置
            LoadConfiguration();
            
            // 启动定时索引服务
            _scheduledIndexingService.StartScheduledIndexing(60, _viewModel.SelectedIndexPath);
        }

        public void UpdateStatus(string text)
        {
            _viewModel?.UpdateStatus(text);
        }

        public void SetMinimizeOnClose(bool value)
        {
            _minimizeOnClose = value;
        }

        private void PreviewSearchTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var keyword = PreviewSearchTextBox.Text ?? string.Empty;
                // 直接调用HighlightPreviewKeyword方法，避免重复计算
                HighlightPreviewKeyword(keyword);
                e.Handled = true;
            }
        }

        private void PreviousMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_previewMatchRanges == null || _previewMatchRanges.Count == 0) return;
            if (_currentMatchIndex <= 0) _currentMatchIndex = _previewMatchRanges.Count - 1; else _currentMatchIndex--;
            var item = _previewMatchRanges[_currentMatchIndex];
            PreviewRichTextBox.Selection.Select(item.start, item.end);
            PreviewRichTextBox.Focus();
            // 将匹配项居中显示
            ScrollToCenterOfViewport(item.start);
            // 更新匹配计数显示
            MatchCountTextBlock.Text = $"{_currentMatchIndex + 1}/{_previewMatchRanges.Count}";
        }

        private void NextMatch_Click(object sender, RoutedEventArgs e)
        {
            if (_previewMatchRanges == null || _previewMatchRanges.Count == 0) return;
            if (_currentMatchIndex >= _previewMatchRanges.Count - 1) _currentMatchIndex = 0; else _currentMatchIndex++;
            var item = _previewMatchRanges[_currentMatchIndex];
            PreviewRichTextBox.Selection.Select(item.start, item.end);
            PreviewRichTextBox.Focus();
            // 将匹配项居中显示
            ScrollToCenterOfViewport(item.start);
            // 更新匹配计数显示
            MatchCountTextBlock.Text = $"{_currentMatchIndex + 1}/{_previewMatchRanges.Count}";
        }
        
        /// <summary>
        /// 将指定位置滚动到视口中心
        /// </summary>
        /// <param name="position">要居中显示的位置</param>
        private void ScrollToCenterOfViewport(TextPointer position)
        {
            // 获取内容矩形
            var contentRect = position.GetCharacterRect(LogicalDirection.Forward);
            
            // 获取滚动查看器
            var scrollViewer = FindVisualChild<ScrollViewer>(PreviewRichTextBox);
            if (scrollViewer == null) return;
            
            // 计算需要滚动到的位置，使内容居中
            var verticalOffset = contentRect.Top - (scrollViewer.ViewportHeight / 2) + (contentRect.Height / 2);
            
            // 确保滚动位置在有效范围内
            verticalOffset = Math.Max(0, Math.Min(verticalOffset, scrollViewer.ScrollableHeight));
            
            // 滚动到计算出的位置
            scrollViewer.ScrollToVerticalOffset(verticalOffset);
        }
        
        /// <summary>
        /// 查找可视化树中的指定类型的子元素
        /// </summary>
        /// <typeparam name="T">要查找的类型</typeparam>
        /// <param name="depObj">起始依赖对象</param>
        /// <returns>找到的子元素，如果未找到则返回null</returns>
        private static T FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = child as T ?? FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewModel?.SelectedResult;
            var path = selected?.FilePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    var psi = new ProcessStartInfo("explorer.exe", $"/select, \"{path}\"")
                    {
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch
                {
                    WPFMessageBox.Show("无法打开文件所在位置", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var selected = _viewModel?.SelectedResult;
            var path = selected?.FilePath ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    WindowsFormsClipboard.SetText(path);
                    _viewModel?.UpdateStatus("路径已复制到剪贴板");
                }
                catch
                {
                    WPFMessageBox.Show("复制路径失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Preferences_Main_Click(object sender, RoutedEventArgs e)
        {
            var win = new PreferencesWindow(
                _scheduledIndexingService,
                _indexStorageService,
                this,
                _hotKeyService,
                () => _viewModel?.ToggleWindowCommand?.Execute(null)
            );
            win.Owner = this;
            win.ShowDialog();
        }

        private void OpenIndexManagement_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.IndexManagementWindow(_indexStorageService);
            win.Owner = this;
            win.ShowDialog();
        }

        private void CleanupInvalidRecords_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var countTask = _indexStorageService.CleanupInvalidRecordsAsync();
                countTask.ContinueWith(t =>
                {
                    var count = t.Result;
                    Dispatcher.Invoke(() =>
                    {
                        WPFMessageBox.Show($"已清理 {count} 条无效记录", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        _viewModel?.UpdateStatus($"已清理 {count} 条无效记录");
                    });
                });
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"清理无效记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateTestData_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.GenerateTestDataCommand?.Execute(null);
        }

        private void ScheduledIndexing_PresetInterval_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && int.TryParse(mi.Tag?.ToString(), out var minutes))
            {
                _scheduledIndexingService?.StartScheduledIndexing(minutes, _viewModel?.SelectedIndexPath ?? string.Empty);
                WPFMessageBox.Show($"定时索引已设置为每 {minutes} 分钟执行一次", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ScheduledIndexing_CustomInterval_Click(object sender, RoutedEventArgs e)
        {
            var win = new Windows.ScheduledIndexingSettingsWindow(_scheduledIndexingService, _viewModel?.SelectedIndexPath ?? string.Empty);
            win.Owner = this;
            win.ShowDialog();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"; // 明确指定类型
            WPFMessageBox.Show($"文件内容索引与搜索工具\n版本: {ver}", "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SearchText = SearchTextBox.Text ?? string.Empty;
            _viewModel.SearchCommand?.Execute(null);
            try
            {
                var kw = _viewModel.SearchText ?? string.Empty;
                PreviewSearchTextBox.Text = kw;
            }
            catch { }
        }

        private void IndexPathComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.ComboBox comboBox) // 明确指定类型
                {
                    // 清除现有项
                    comboBox.Items.Clear();
                    
                    // 添加"所有磁盘"选项
                    comboBox.Items.Add("所有磁盘");
                    
                    // 添加实际的盘符
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.IsReady)
                        {
                            comboBox.Items.Add(drive.Name);
                        }
                    }
                    
                    // 添加自定义选项
                    comboBox.Items.Add("自定义目录...");
                    
                    // 从配置加载已保存的路径
                    var savedPath = LoadIndexPathFromConfig();
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        // 如果是分号分隔的多个路径，显示为"多个路径"
                        if (savedPath.Contains(";"))
                        {
                            if (!comboBox.Items.Contains("多个路径"))
                            {
                                comboBox.Items.Insert(comboBox.Items.Count - 1, "多个路径");
                            }
                            comboBox.SelectedItem = "多个路径";
                        }
                        else if (Directory.Exists(savedPath))
                        {
                            if (!comboBox.Items.Contains(savedPath))
                            {
                                comboBox.Items.Insert(comboBox.Items.Count - 1, savedPath);
                            }
                            comboBox.SelectedItem = savedPath;
                        }
                        else
                        {
                            comboBox.SelectedIndex = 0; // 默认选择"所有磁盘"
                        }
                    }
                    else
                    {
                        comboBox.SelectedIndex = 0; // 默认选择"所有磁盘"
                    }
                }
            }
            catch { }
        }

        private void IndexPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox && comboBox.SelectedItem != null) // 明确指定类型
            {
                string selectedLocation = comboBox.SelectedItem.ToString();
                
                // 处理自定义选项
                if (selectedLocation == "自定义目录...")
                {
                    // 显示文件夹选择对话框
                    using var dialog = new FolderBrowserDialog
                    {
                        Description = "选择建立索引的目录",
                        SelectedPath = _viewModel?.SelectedIndexPath ?? string.Empty,
                        ShowNewFolderButton = true
                    };
                    
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var selected = dialog.SelectedPath;
                        if (!_viewModel.IndexPaths.Contains(selected))
                        {
                            _viewModel.IndexPaths.Add(selected);
                        }
                        _viewModel.SelectedIndexPath = selected;
                        _configService.SaveIndexPath(selected);
                        
                        // 添加自定义路径到列表
                        if (!comboBox.Items.Contains(selected))
                        {
                            comboBox.Items.Insert(comboBox.Items.Count - 1, selected); // 插入到"自定义目录..."之前
                        }
                        
                        // 选择新添加的路径
                        comboBox.SelectedItem = selected;
                    }
                    else
                    {
                        // 如果用户取消了选择，保持之前的选择
                        var savedPath = LoadIndexPathFromConfig();
                        if (!string.IsNullOrWhiteSpace(savedPath))
                        {
                            comboBox.SelectedItem = savedPath;
                        }
                        else
                        {
                            comboBox.SelectedIndex = 0;
                        }
                    }
                }
                else if (selectedLocation == "所有磁盘")
                {
                    // 处理"所有磁盘"选项，获取所有可用磁盘
                    var drives = DriveInfo.GetDrives()
                        .Where(d => d.IsReady)
                        .Select(d => d.Name.TrimEnd('\\'))
                        .ToList();
                    
                    if (drives.Count > 0)
                    {
                        // 使用分号连接所有磁盘
                        var allPaths = string.Join(";", drives);
                        _viewModel.SelectedIndexPath = allPaths;
                        _configService.SaveIndexPath(allPaths);
                    }
                }
                else if (selectedLocation == "多个路径")
                {
                    // 保持当前的多个路径设置
                    var savedPath = LoadIndexPathFromConfig();
                    if (!string.IsNullOrWhiteSpace(savedPath))
                    {
                        _viewModel.SelectedIndexPath = savedPath;
                    }
                }
                else
                {
                    // 处理普通路径选择
                    _viewModel.SelectedIndexPath = selectedLocation.TrimEnd('\\');
                    _configService.SaveIndexPath(selectedLocation.TrimEnd('\\'));
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "选择建立索引的目录",
                    SelectedPath = _viewModel?.SelectedIndexPath ?? string.Empty,
                    ShowNewFolderButton = true
                };
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selected = dialog.SelectedPath;
                    if (!_viewModel.IndexPaths.Contains(selected))
                    {
                        _viewModel.IndexPaths.Add(selected);
                    }
                    _viewModel.SelectedIndexPath = selected;
                    _configService.SaveIndexPath(selected);
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"选择索引目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ResultsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel?.SelectedResult == null) return;
            try
            {
                var filePath = _viewModel.SelectedResult.FilePath;
                var content = await _searchService.GetFileContentAsync(filePath) ?? string.Empty;
                _viewModel.PreviewContent = content;

                var paragraph = new Paragraph();
                var ext = global::System.IO.Path.GetExtension(filePath);
                var lang = SyntaxHighlightService.GetLanguageByExtension(ext);
                SyntaxHighlightService.ApplySyntaxHighlight(paragraph, content, lang);
                var doc = new FlowDocument(paragraph);
                doc.PagePadding = new Thickness(0);
                doc.ColumnWidth = double.PositiveInfinity;
                doc.TextAlignment = TextAlignment.Left;
                PreviewRichTextBox.Document = doc;
                MatchCountTextBlock.Text = string.Empty;
                try
                {
                    var kw = _viewModel.SearchText ?? string.Empty;
                    PreviewSearchTextBox.Text = kw;
                    HighlightPreviewKeyword(kw);
                }
                catch { }
            }
            catch (Exception ex)
            {
                _viewModel.PreviewContent = string.Empty;
                PreviewRichTextBox.Document = new FlowDocument(new Paragraph(new Run("预览加载失败")));
                _notificationService?.ShowError($"预览加载失败: {ex.Message}");
            }
        }

        private void HighlightPreviewKeyword(string keyword)
        {
            var doc = PreviewRichTextBox?.Document;
            if (doc == null) return;
            
            // 清除之前的高亮显示
            if (_previewMatchRanges != null && _previewMatchRanges.Count > 0)
            {
                foreach (var rng in _previewMatchRanges)
                {
                    var tr = new TextRange(rng.start, rng.end);
                    tr.ApplyPropertyValue(TextElement.BackgroundProperty, null);
                }
                _previewMatchRanges.Clear();
            }
            
            if (string.IsNullOrWhiteSpace(keyword)) 
            {
                // 清空匹配计数显示
                MatchCountTextBlock.Text = string.Empty;
                _currentMatchIndex = -1;
                return;
            }
            
            // 回退到更简单可靠的实现方式
            var pos = doc.ContentStart;
            int count = 0;
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 236, 153));
            int highlighted = 0;
            int maxHighlight = 300;
            
            while (pos != null && pos.CompareTo(doc.ContentEnd) < 0)
            {
                var runText = pos.GetTextInRun(LogicalDirection.Forward);
                if (!string.IsNullOrEmpty(runText))
                {
                    int idx = 0;
                    while (idx < runText.Length)
                    {
                        int found = runText.IndexOf(keyword, idx, StringComparison.OrdinalIgnoreCase);
                        if (found < 0) break;
                        
                        var start = pos.GetPositionAtOffset(found, LogicalDirection.Forward);
                        var end = start?.GetPositionAtOffset(keyword.Length, LogicalDirection.Forward);
                        
                        if (start != null && end != null)
                        {
                            var range = new TextRange(start, end);
                            if (highlighted < maxHighlight)
                            {
                                range.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
                                highlighted++;
                            }
                            _previewMatchRanges.Add((start, end));
                            count++;
                        }
                        
                        idx = found + Math.Max(1, keyword.Length); // 确保至少前进1个字符
                    }
                    pos = pos.GetPositionAtOffset(runText.Length, LogicalDirection.Forward);
                }
                else
                {
                    pos = pos.GetNextContextPosition(LogicalDirection.Forward);
                }
            }
            
            // 更新匹配计数显示
            MatchCountTextBlock.Text = count > 0 ? $"1/{count}" : string.Empty;
            _currentMatchIndex = count > 0 ? 0 : -1;
        }
        
        /*
        /// <summary>
        /// 在FlowDocument中查找指定文本的所有匹配项
        /// </summary>
        /// <param name="document">要搜索的文档</param>
        /// <param name="searchText">要查找的文本</param>
        /// <returns>匹配项列表</returns>
        private List<(TextRange range, int position)> FindTextInFlowDocument(FlowDocument document, string searchText)
        {
            var matches = new List<(TextRange range, int position)>();
            if (document == null || string.IsNullOrWhiteSpace(searchText)) return matches;
            
            // 获取文档的完整文本
            var documentText = new TextRange(document.ContentStart, document.ContentEnd).Text;
            
            // 查找所有匹配项
            int index = 0;
            while ((index = documentText.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                // 计算匹配项在文档中的实际位置
                var startPointer = document.ContentStart.GetPositionAtOffset(index);
                var endPointer = startPointer?.GetPositionAtOffset(searchText.Length);
                
                if (startPointer != null && endPointer != null)
                {
                    matches.Add((new TextRange(startPointer, endPointer), index));
                }
                
                index += searchText.Length;
            }
            
            return matches;
        }
        */

        private void ResultsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = _viewModel?.SelectedResult;
            var path = selected?.FilePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    var psi = new ProcessStartInfo(path)
                    {
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch
                {
                    WPFMessageBox.Show("无法打开文件", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            // 预留：界面动画帧更新
        }

        private void LoadConfiguration()
        {
            try
            {
                // 预加载索引统计缓存（提升管理页打开速度）
                _ = IndexStatisticsCache.Instance.PreloadAsync();
            }
            catch { }
            try
            {
                // 注册全局快捷键（若已配置）
                Windows.HotKeySettingsWindow.RegisterHotKeyFromConfig(this, () => _viewModel?.ToggleWindowCommand?.Execute(null));
            }
            catch { }
            try
            {
                // 加载索引路径到下拉框
                var path = LoadIndexPathFromConfig();
                if (!string.IsNullOrWhiteSpace(path) && !_viewModel.IndexPaths.Contains(path))
                {
                    _viewModel.IndexPaths.Add(path);
                }
            }
            catch { }
        }

        private string LoadIndexPathFromConfig()
        {
            try
            {
                var cfg = global::System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.config");
                if (File.Exists(cfg))
                {
                    var lines = File.ReadAllLines(cfg);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("IndexPath=", StringComparison.OrdinalIgnoreCase))
                        {
                            return line.Substring("IndexPath=".Length).Trim();
                        }
                    }
                }
            }
            catch { }
            return string.Empty;
        }
    }
}
