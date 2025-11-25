using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using FileSearchTool.Data;
using FileSearchTool.Model;
using FileSearchTool.Services;
using WPFApplication = System.Windows.Application; // 明确指定WPF的Application

namespace FileSearchTool.ViewModel
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region 字段和属性

        private readonly NotificationService _notificationService;
        
        // 服务依赖
        public SearchService SearchService { get; set; }
        public IndexingService IndexingService { get; set; }
        public IndexStorageService IndexStorageService { get; set; }
        public DatabaseConfigService ConfigService { get; set; }

        // 搜索相关
        private string _searchText = string.Empty;
        private ObservableCollection<SearchResultViewModel> _searchResults;
        private SearchResultViewModel? _selectedResult;

        // 索引相关
        private string _selectedIndexPath = string.Empty;
        private ObservableCollection<string> _indexPaths;
        private bool _isIndexing = false;
        private int _indexingProgressValue = 0;
        private string _indexingStatus = string.Empty;
        private int _processedFiles = 0;
        private int _totalFiles = 0;
        private CancellationTokenSource _cancellationTokenSource;

        // 文件类型过滤
        private ObservableCollection<string> _includedFileTypes;
        private ObservableCollection<string> _excludedFileTypes;
        private string _selectedFileType = "全部";

        // 状态信息
        private string _statusText = string.Empty;
        private int _resultCount = 0;
        private string _previewContent = string.Empty;
        private bool _isKeywordTooShort = false;

        // 性能设置
        private int _maxConcurrentIndexingTasks = 4;
        private int _chunkSize = 100;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    _isKeywordTooShort = (_searchText ?? string.Empty).Trim().Length < 2;
                    OnPropertyChanged(nameof(IsKeywordTooShort));
                    OnPropertyChanged();
                    _ = SearchAsyncDebounced();
                }
            }
        }

        public ObservableCollection<SearchResultViewModel> SearchResults
        {
            get => _searchResults;
            set
            {
                _searchResults = value;
                OnPropertyChanged();
                ResultCount = value.Count;
            }
        }

        public SearchResultViewModel? SelectedResult
        {
            get => _selectedResult;
            set
            {
                _selectedResult = value;
                OnPropertyChanged();
            }
        }

        public string SelectedIndexPath
        {
            get => _selectedIndexPath;
            set
            {
                if (_selectedIndexPath != value)
                {
                    _selectedIndexPath = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<string> IndexPaths
        {
            get => _indexPaths;
            set
            {
                _indexPaths = value;
                OnPropertyChanged();
            }
        }

        public bool IsIndexing
        {
            get => _isIndexing;
            set
            {
                _isIndexing = value;
                OnPropertyChanged();
            }
        }

        public int IndexingProgressValue
        {
            get => _indexingProgressValue;
            set
            {
                _indexingProgressValue = value;
                OnPropertyChanged();
            }
        }

        public string IndexingStatus
        {
            get => _indexingStatus;
            set
            {
                _indexingStatus = value;
                OnPropertyChanged();
            }
        }

        public int ProcessedFiles
        {
            get => _processedFiles;
            set
            {
                _processedFiles = value;
                OnPropertyChanged();
            }
        }

        public int TotalFiles
        {
            get => _totalFiles;
            set
            {
                _totalFiles = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> IncludedFileTypes
        {
            get => _includedFileTypes;
            set
            {
                _includedFileTypes = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> ExcludedFileTypes
        {
            get => _excludedFileTypes;
            set
            {
                _excludedFileTypes = value;
                OnPropertyChanged();
            }
        }

        public string SelectedFileType
        {
            get => _selectedFileType;
            set
            {
                _selectedFileType = value;
                OnPropertyChanged();
                _ = SearchAsyncDebounced();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public int ResultCount
        {
            get => _resultCount;
            set
            {
                _resultCount = value;
                OnPropertyChanged();
            }
        }

        public string PreviewContent
        {
            get => _previewContent;
            set
            {
                _previewContent = value;
                OnPropertyChanged();
            }
        }

        public bool IsKeywordTooShort
        {
            get => _isKeywordTooShort;
            set
            {
                _isKeywordTooShort = value;
                OnPropertyChanged();
            }
        }

        public int MaxConcurrentIndexingTasks
        {
            get => _maxConcurrentIndexingTasks;
            set
            {
                _maxConcurrentIndexingTasks = value;
                OnPropertyChanged();
            }
        }

        public int ChunkSize
        {
            get => _chunkSize;
            set
            {
                _chunkSize = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region 命令

        public ICommand SearchCommand { get; }
        public ICommand StartIndexingCommand { get; }
        public ICommand CancelIndexingCommand { get; }
        public ICommand PreviewFileCommand { get; }
        public ICommand GenerateTestDataCommand { get; }
        public ICommand ToggleWindowCommand { get; }

        #endregion

        public MainViewModel(NotificationService notificationService = null)
        {
            _notificationService = notificationService;
            
            // 初始化命令
            SearchCommand = new AsyncRelayCommand(async () => await SearchAsync());
            StartIndexingCommand = new AsyncRelayCommand(async () => await StartIndexingAsync());
            CancelIndexingCommand = new RelayCommand(CancelIndexing);
            PreviewFileCommand = new AsyncRelayCommand<object>(async (parameter) => await PreviewFile(parameter));
            GenerateTestDataCommand = new AsyncRelayCommand(async () => await GenerateTestDataAsync());
            ToggleWindowCommand = new RelayCommand(ToggleWindow);
            
            // 初始化集合
            _searchResults = new ObservableCollection<SearchResultViewModel>();
            _indexPaths = new ObservableCollection<string>();
            _includedFileTypes = new ObservableCollection<string>();
            _excludedFileTypes = new ObservableCollection<string>();
            
            // 启用集合的线程安全访问
            BindingOperations.EnableCollectionSynchronization(_searchResults, new object());
        }

        #region 公共方法

        /// <summary>
        /// 更新状态文本
        /// </summary>
        public void UpdateStatus(string message)
        {
            StatusText = message;
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行搜索
        /// </summary>
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchResults.Clear();
                return;
            }

            try
            {
                StatusText = "正在搜索...";
                var results = await SearchService.SearchAsync(SearchText);
                
                SearchResults.Clear();
                foreach (var result in results.Select((r, index) => new { Result = r, Index = index }))
                {
                    SearchResults.Add(new SearchResultViewModel
                    {
                        Id = result.Result.Id,
                        FileName = result.Result.FileName,
                        FilePath = result.Result.FilePath,
                        FileSize = result.Result.FileSize,
                        LastModified = result.Result.LastModified,
                        Snippet = result.Result.Snippet,
                        Rank = result.Index + 1
                    });
                }
                
                StatusText = $"找到 {SearchResults.Count} 个结果";
            }
            catch (Exception ex)
            {
                StatusText = $"搜索出错: {ex.Message}";
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"搜索失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 执行防抖搜索（输入时调用）
        /// </summary>
        private async Task SearchAsyncDebounced()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchResults.Clear();
                return;
            }
            try
            {
                StatusText = "正在搜索...";
                var results = await SearchService.SearchWithDebounce(SearchText, SelectedFileType);
                SearchResults.Clear();
                foreach (var result in results.Select((r, index) => new { Result = r, Index = index }))
                {
                    SearchResults.Add(new SearchResultViewModel
                    {
                        Id = result.Result.Id,
                        FileName = result.Result.FileName,
                        FilePath = result.Result.FilePath,
                        FileSize = result.Result.FileSize,
                        LastModified = result.Result.LastModified,
                        Snippet = result.Result.Snippet,
                        Rank = result.Index + 1
                    });
                }
                StatusText = $"找到 {SearchResults.Count} 个结果";
            }
            catch (Exception ex)
            {
                StatusText = $"搜索出错: {ex.Message}";
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"搜索失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 开始索引
        /// </summary>
        private async Task StartIndexingAsync()
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileSearchTool", "index_debug.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            
            void Log(string msg)
            {
                try
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
                }
                catch { }
            }
            
            try
            {
                Log("StartIndexingAsync 开始执行");
                // 若未选择索引路径，提示并退出
                if (string.IsNullOrWhiteSpace(SelectedIndexPath))
                {
                    if (_notificationService != null)
                    {
                        _notificationService.ShowWarning("请先选择索引位置");
                    }
                    Log("未选择索引路径，退出");
                    return;
                }

                // 检查路径是否存在（支持多个路径）
                bool pathExists = true;
                string errorMessage = "";
                
                if (SelectedIndexPath.Contains(";"))
                {
                    // 多个路径的情况
                    var paths = SelectedIndexPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();
                    
                    foreach (var path in paths)
                    {
                        if (!Directory.Exists(path))
                        {
                            pathExists = false;
                            errorMessage = $"选择的索引路径不存在: {path}";
                            break;
                        }
                    }
                }
                else
                {
                    // 单个路径的情况
                    if (!Directory.Exists(SelectedIndexPath))
                    {
                        pathExists = false;
                        errorMessage = "选择的索引路径不存在";
                    }
                }

                if (!pathExists)
                {
                    if (_notificationService != null)
                    {
                        _notificationService.ShowWarning(errorMessage);
                    }
                    Log($"{errorMessage}，退出");
                    return;
                }

                // 设置索引状态
                IsIndexing = true;
                IndexingProgressValue = 0;
                ProcessedFiles = 0;
                TotalFiles = 0;
                IndexingStatus = "正在准备索引...";
                Log($"开始索引路径: {SelectedIndexPath}");

                // 创建取消令牌
                _cancellationTokenSource = new CancellationTokenSource();

                // 记录索引路径到配置
                ConfigService?.SaveIndexPath(SelectedIndexPath);
                Log("保存索引路径到配置");

                // 执行索引
                await IndexingService.IndexDirectoryAsync(
                    SelectedIndexPath,
                    _cancellationTokenSource.Token,
                    progress: new Progress<IndexingProgress>(ReportIndexingProgress),
                    maxConcurrentTasks: MaxConcurrentIndexingTasks,
                    chunkSize: ChunkSize
                );

                // 索引完成
                IsIndexing = false;
                IndexingStatus = "索引完成";
                StatusText = "索引完成";
                Log("索引完成");
                
                if (_notificationService != null)
                {
                    _notificationService.ShowSuccess("索引完成");
                }
            }
            catch (OperationCanceledException)
            {
                // 用户取消了索引
                IsIndexing = false;
                IndexingStatus = "索引已取消";
                StatusText = "索引已取消";
                Log("索引被取消");
                
                if (_notificationService != null)
                {
                    _notificationService.ShowInfo("索引已取消");
                }
            }
            catch (Exception ex)
            {
                // 索引出错
                IsIndexing = false;
                IndexingStatus = "索引出错";
                StatusText = $"索引出错: {ex.Message}";
                Log($"索引出错: {ex}");
                
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"索引失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 报告索引进度
        /// </summary>
        private void ReportIndexingProgress(IndexingProgress progress)
        {
            WPFApplication.Current.Dispatcher.Invoke(() =>
            {
                ProcessedFiles = progress.ProcessedFiles;
                TotalFiles = progress.TotalFiles;
                IndexingProgressValue = progress.ProgressPercentage;
                IndexingStatus = progress.Status;
                StatusText = progress.Status;
            });
        }

        /// <summary>
        /// 取消索引
        /// </summary>
        private void CancelIndexing()
        {
            if (IsIndexing && _cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// 预览文件
        /// </summary>
        private async Task PreviewFile(object parameter)
        {
            if (parameter is SearchResultViewModel result)
            {
                try
                {
                    StatusText = "正在加载预览...";
                    var content = await SearchService.GetFileContentAsync(result.FilePath);
                    PreviewContent = content ?? string.Empty;
                    StatusText = "预览加载完成";
                }
                catch (Exception ex)
                {
                    PreviewContent = string.Empty;
                    StatusText = $"预览加载失败: {ex.Message}";
                    if (_notificationService != null)
                    {
                        _notificationService.ShowError($"预览加载失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 生成测试数据
        /// </summary>
        private async Task GenerateTestDataAsync()
        {
            try
            {
                StatusText = "正在生成测试数据...";
                await Task.Run(() =>
                {
                    var testDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileSearchTool_TestData");
                    Directory.CreateDirectory(testDir);

                    var testFiles = new[]
                    {
                        "测试文档1.txt",
                        "测试文档2.txt",
                        "测试文档3.txt"
                    };

                    var testContents = new[]
                    {
                        "这是第一个测试文档的内容，包含一些关键词如：测试、文档、搜索。",
                        "这是第二个测试文档的内容，也包含关键词：测试、文件、索引。",
                        "这是第三个测试文档的内容，同样有关键词：测试、内容、预览。"
                    };

                    for (int i = 0; i < testFiles.Length; i++)
                    {
                        var filePath = Path.Combine(testDir, testFiles[i]);
                        File.WriteAllText(filePath, testContents[i], System.Text.Encoding.UTF8);
                    }
                });

                StatusText = "测试数据生成完成";
                if (_notificationService != null)
                {
                    _notificationService.ShowSuccess("测试数据已生成到文档文件夹中的 FileSearchTool_TestData 目录");
                }
            }
            catch (Exception ex)
            {
                StatusText = $"生成测试数据失败: {ex.Message}";
                if (_notificationService != null)
                {
                    _notificationService.ShowError($"生成测试数据失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 切换窗口显示状态
        /// </summary>
        private void ToggleWindow()
        {
            var mainWindow = WPFApplication.Current.MainWindow;
            if (mainWindow != null)
            {
                if (mainWindow.IsVisible)
                {
                    mainWindow.Hide();
                }
                else
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            }
        }

        #endregion

        #region INotifyPropertyChanged实现

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
