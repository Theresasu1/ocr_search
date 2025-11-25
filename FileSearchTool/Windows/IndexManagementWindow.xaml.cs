using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using FileSearchTool.Services;
using FileSearchTool.Data;
using Microsoft.EntityFrameworkCore;
using WPFMessageBox = System.Windows.MessageBox;

namespace FileSearchTool.Windows
{
    public partial class IndexManagementWindow : Window
    {
        private readonly IndexStorageService _indexStorage;
        private string _databasePath = "";
        private NotificationService _notificationService; // 添加通知服务字段

        public IndexManagementWindow(IndexStorageService indexStorage)
        {
            // 极速初始化，只设置必需属性
            InitializeComponent();
            _indexStorage = indexStorage;
            
            // 初始化通知服务
            _notificationService = new NotificationService(this);
            
            // 计算数据库路径（使用配置服务）
            _databasePath = DatabaseConfigService.GetDatabaseFilePath();
            DatabasePathTextBlock.Text = _databasePath;
        }
        
        /// <summary>
        /// 窗口显示后才开始加载数据
        /// </summary>
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
        }
        
        /// <summary>
        /// 异步加载数据
        /// </summary>
        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            try
            {
                // 先尝试同步获取缓存
                var cachedStats = IndexStatisticsCache.Instance.GetCachedStatisticsSync();
                if (cachedStats != null)
                {
                    Console.WriteLine("使用缓存数据");
                    DisplayStatistics(cachedStats);
                    return;
                }
                
                // 无缓存，异步加载
                Console.WriteLine("无缓存，开始加载...");
                await LoadIndexInfoAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载数据失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 显示统计信息
        /// </summary>
        private void DisplayStatistics(IndexStatistics statistics)
        {
            // 此处不再显示文件数、数据库大小和最后索引时间
        }

        private async System.Threading.Tasks.Task LoadIndexInfoAsync()
        {
            try
            {
                // 使用缓存服务获取统计信息（快速响应）
                var statistics = await IndexStatisticsCache.Instance.GetStatisticsAsync();
                
                // 更新 UI
                DisplayStatistics(statistics);
                
                if (statistics.HasError)
                {
                    WPFMessageBox.Show($"加载索引信息失败: {statistics.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"加载索引信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteIndex_Click(object sender, RoutedEventArgs e)
        {
            var result = WPFMessageBox.Show(
                "确定要删除所有索引数据吗？\n\n此操作不可恢复，删除后需要重新建立索引。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 禁用按钮防止重复点击
                DeleteButton.IsEnabled = false;
                SetPathButton.IsEnabled = false;
                
                try
                {
                    // 在后台线程执行删除操作，避免UI卡死
                    await System.Threading.Tasks.Task.Run(async () =>
                    {
                        // 执行清空操作
                        await _indexStorage.ClearDatabaseAsync();
                        
                        // 执行VACUUM回收数据库空间
                        await VacuumDatabaseAsync();
                    });
                    
                    // 使用通知服务显示成功信息
                    if (_notificationService != null)
                    {
                        _notificationService.ShowSuccess("索引已成功删除。", 3);
                    }
                    else
                    {
                        WPFMessageBox.Show(
                            "索引已成功删除。", 
                            "成功", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                    
                    // 使缓存失效
                    IndexStatisticsCache.Instance.InvalidateCache();
                }
                catch (Exception ex)
                {
                    // 使用通知服务显示错误信息
                    if (_notificationService != null)
                    {
                        _notificationService.ShowError($"删除索引失败: {ex.Message}");
                    }
                    else
                    {
                        WPFMessageBox.Show(
                            $"删除索引失败: {ex.Message}", 
                            "错误", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                    }
                }
                finally
                {
                    // 重新启用按钮
                    DeleteButton.IsEnabled = true;
                    SetPathButton.IsEnabled = true;
                }
            }
        }
        
        /// <summary>
        /// 执行VACUUM操作回收数据库空间
        /// </summary>
        private async System.Threading.Tasks.Task VacuumDatabaseAsync()
        {
            try
            {
                var dbContext = new SearchDbContext();
                await dbContext.Database.ExecuteSqlRawAsync("VACUUM;");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"VACUUM执行失败: {ex.Message}");
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    // 打开数据库所在文件夹并选中该文件
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_databasePath}\"");
                }
                else
                {
                    // 文件不存在，打开上级目录
                    var directory = Path.GetDirectoryName(_databasePath);
                    if (Directory.Exists(directory))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", directory);
                    }
                    else
                    {
                        WPFMessageBox.Show("数据库文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"打开文件夹失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 浏览数据库存储位置
        /// </summary>
        private void BrowseDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "选择数据库存储位置",
                    SelectedPath = DatabaseConfigService.GetDatabaseDirectory(),
                    ShowNewFolderButton = true
                };
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedPath = dialog.SelectedPath;
                    
                    // 检查磁盘空间
                    var freeSpaceGB = DatabaseConfigService.GetDiskFreeSpaceGB(selectedPath);
                    if (freeSpaceGB < 1.0) // 小于 1GB
                    {
                        var result = WPFMessageBox.Show(
                            $"所选磁盘剩余空间仅剩 {freeSpaceGB:F2} GB，可能不够存储索引数据。\n\n是否继续？",
                            "磁盘空间不足",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        
                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                    
                    // 设置新路径
                    if (DatabaseConfigService.SetDatabasePath(selectedPath))
                    {
                        DatabasePathTextBlock.Text = selectedPath;
                        UpdateDiskSpace(selectedPath);
                        
                        // 提示需要重启
                        WPFMessageBox.Show(
                            "数据库存储位置已更改，请重启程序以使设置生效。\n\n新路径：" + selectedPath,
                            "需要重启",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        WPFMessageBox.Show(
                            "设置数据库路径失败，请确保该目录具有读写权限。",
                            "错误",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                WPFMessageBox.Show($"选择数据库路径失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 更新磁盘空间显示
        /// </summary>
        private void UpdateDiskSpace(string path)
        {
            // 不再显示磁盘空间信息
        }
    }
}
