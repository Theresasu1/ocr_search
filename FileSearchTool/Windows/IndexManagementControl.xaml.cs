using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using FileSearchTool.Services;
using FileSearchTool.Data;
using Microsoft.EntityFrameworkCore;
using WPFMessageBox = System.Windows.MessageBox;
using WPFUserControl = System.Windows.Controls.UserControl;

namespace FileSearchTool.Windows
{
    public partial class IndexManagementControl : WPFUserControl
    {
        private readonly IndexStorageService _indexStorage;
        private NotificationService? _notificationService; // 添加可空引用修饰符

        public IndexManagementControl(IndexStorageService indexStorage)
        {
            InitializeComponent();
            _indexStorage = indexStorage;
            
            // 尝试获取主窗口以初始化通知服务
            var mainWindow = Window.GetWindow(this);
            if (mainWindow != null)
            {
                _notificationService = new NotificationService(mainWindow);
            }
        }

        private void LoadIndexInfo()
        {
            try
            {
                var dbPath = DatabaseConfigService.GetDatabaseFilePath();
                DatabasePathTextBlock.Text = dbPath;
            }
            catch
            {
                DatabasePathTextBlock.Text = "未知";
            }
        }

        private void SetPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "选择索引数据库存储位置",
                    SelectedPath = DatabaseConfigService.GetDatabaseDirectory(),
                    ShowNewFolderButton = true
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var selectedPath = dialog.SelectedPath;

                    // 设置新路径
                    if (DatabaseConfigService.SetDatabasePath(selectedPath))
                    {
                        DatabasePathTextBlock.Text = selectedPath;
                        
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
                    
                    // 刷新信息
                    LoadIndexInfo();
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
    }
}