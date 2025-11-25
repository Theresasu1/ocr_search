using FileSearchTool.Data;
using FileSearchTool.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;
using WpfStartupEventArgs = System.Windows.StartupEventArgs;
using SQLitePCL; // 新增：确保使用 e_sqlite3 并启用 FTS5

namespace FileSearchTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private SearchDbContext? _dbContext;
        private ContentExtractorService? _contentExtractor;
        private IndexStorageService? _indexStorage;

        protected override void OnStartup(WpfStartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);

                // 初始化 SQLitePCLRaw 的 e_sqlite3 提供者，确保启用 FTS5
                try
                {
                    Batteries_V2.Init();
                }
                catch (Exception ex)
                {
                    LogError(ex, "初始化 SQLitePCLRaw 失败（继续启动）");
                }

                // 注册CodePagesEncodingProvider以支持更多编码格式
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                }
                catch (Exception ex)
                {
                    LogError(ex, "注册 CodePagesEncodingProvider 失败（继续启动）");
                }

                // 添加全局异常处理
                this.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // 异步初始化数据库，不阻塞UI线程启动
                _ = Task.Run(async () => await InitializeDatabaseAsync());
            }
            catch (Exception ex)
            {
                LogError(ex, "应用程序启动时出错");
                WpfMessageBox.Show($"应用程序启动时出错: {ex.Message}\n{ex.StackTrace}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                // 移除：不要自动退出，允许用户继续使用界面
                // Shutdown();
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            try
            {
                // 创建 DbContext
                var optionsBuilder = new DbContextOptionsBuilder<SearchDbContext>();
                var dbPath = Services.DatabaseConfigService.GetDatabaseFilePath();
                var dbDir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(dbDir) && !Directory.Exists(dbDir))
                {
                    Directory.CreateDirectory(dbDir);
                }
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                
                _dbContext = new SearchDbContext(optionsBuilder.Options);
                _contentExtractor = new ContentExtractorService();
                _indexStorage = new IndexStorageService(_dbContext, _contentExtractor);
                
                // 确保数据库已创建
                await _dbContext.Database.EnsureCreatedAsync();

                // 初始化FTS5虚拟表并同步数据
                try
                {
                    await _dbContext.InitializeAsync();
                }
                catch (Exception ex)
                {
                    LogError(ex, "初始化FTS5全文索引失败（继续启动）");
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "初始化数据库时出错");
                WpfMessageBox.Show($"初始化数据库时出错: {ex.Message}\n{ex.StackTrace}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                throw; // 重新抛出异常，确保应用程序正确处理
            }
        }

        // 处理UI线程上的未处理异常
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogError(e.Exception, "UI线程未处理异常");
            WpfMessageBox.Show($"应用程序发生错误: {e.Exception.Message}\n{e.Exception.StackTrace}", "错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
            e.Handled = true; // 标记异常为已处理，防止应用程序崩溃
        }

        // 处理非UI线程上的未处理异常
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                LogError(ex, "非UI线程未处理异常");
                // 在非UI线程上显示消息框需要使用Dispatcher
                this.Dispatcher.Invoke(() =>
                {
                    WpfMessageBox.Show($"应用程序发生严重错误: {ex.Message}\n{ex.StackTrace}", "严重错误", WpfMessageBoxButton.OK, WpfMessageBoxImage.Error);
                });
            }
        }

        // 记录错误到文件
        private void LogError(Exception ex, string errorType)
        {
            try
            {
                string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error_log.txt");
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] {errorType}:");
                    writer.WriteLine($"消息: {ex.Message}");
                    writer.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        writer.WriteLine($"内部异常: {ex.InnerException.Message}");
                        writer.WriteLine($"内部异常堆栈: {ex.InnerException.StackTrace}");
                    }
                    writer.WriteLine(new string('-', 50));
                }
            }
            catch { /* 忽略日志记录本身的错误 */ }
        }
    }
}