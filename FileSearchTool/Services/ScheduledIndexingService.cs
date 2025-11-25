using System;
using System.Threading;
using System.Threading.Tasks;
using FileSearchTool.Data;
using ThreadingTimer = System.Threading.Timer;
using System.Collections.Generic;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 定时索引服务
    /// </summary>
    public class ScheduledIndexingService
    {
        private readonly IndexingService _indexingService;
        private ThreadingTimer? _timer;
        private bool _isRunning = false;

        public ScheduledIndexingService(IndexingService indexingService)
        {
            _indexingService = indexingService;
        }

        // 新增：进度事件
        public event EventHandler<IndexingProgress>? ProgressChanged;

        // 新增：转发文件类型白名单设置
        public void SetAllowedExtensions(IEnumerable<string> extensions)
        {
            _indexingService.SetAllowedExtensions(extensions);
        }

        // 新增：转发是否索引所有文件设置
        public void SetIndexAllFiles(bool indexAll)
        {
            _indexingService.SetIndexAllFiles(indexAll);
        }

        // 新增：获取当前文件类型白名单
        public IEnumerable<string> GetAllowedExtensions()
        {
            return _indexingService.GetAllowedExtensions();
        }

        // 新增：获取当前是否索引所有文件设置
        public bool GetIndexAllFiles()
        {
            return _indexingService.GetIndexAllFiles();
        }

        // 新增：最大文件大小设置
        public void SetMaxFileSize(long maxSize)
        {
            _indexingService.SetMaxFileSize(maxSize);
        }

        public long GetMaxFileSize()
        {
            return _indexingService.GetMaxFileSize();
        }

        // 新增：排除指定目录设置
        public void SetExcludedSubdirectories(IEnumerable<string> paths)
        {
            _indexingService.SetExcludedSubdirectories(paths);
        }
        
        // 新增：获取 IndexStorage 实例，用于索引管理
        public IndexStorageService GetIndexStorage()
        {
            return _indexingService.GetIndexStorage();
        }
        
        // 新增：设置CPU核心数
        public void SetIndexingCores(int cores)
        {
            _indexingService.SetIndexingCores(cores);
        }

        /// <summary>
        /// 启动定时索引任务
        /// </summary>
        /// <param name="interval">间隔时间（分钟）</param>
        /// <param name="pathToIndex">要索引的路径</param>
        public void StartScheduledIndexing(int interval, string pathToIndex)
        {
            if (_isRunning) return;

            _isRunning = true;
            
            // 设置定时器，在指定时间后才开始第一次索引，然后按指定间隔执行
            _timer = new ThreadingTimer(async (state) => await PerformIndexing(pathToIndex), 
                              null, 
                              TimeSpan.FromMinutes(interval),  // 在指定时间后执行第一次
                              TimeSpan.FromMinutes(interval)); // 然后每隔指定时间执行一次
        }

        /// <summary>
        /// 停止定时索引任务
        /// </summary>
        public void StopScheduledIndexing()
        {
            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }

        // 新增：默认间隔启动（例如1小时）
        public void StartWithDefaultInterval(string pathToIndex, int defaultIntervalMinutes = 60)
        {
            StartScheduledIndexing(defaultIntervalMinutes, pathToIndex);
        }

        private async Task PerformIndexing(string pathToIndex)
        {
            // 移除对IsIndexing属性的检查，因为IndexingService中没有这个属性
            // if (_indexingService.IsIndexing) return;

            try
            {
                var cancellationToken = new CancellationToken();
                var progress = new Progress<IndexingProgress>(p => {
                    // 通过事件向外部报告进度
                    ProgressChanged?.Invoke(this, p);
                });

                await _indexingService.StartIndexingAsync(pathToIndex, cancellationToken, progress);
            }
            catch (Exception ex)
            {
                // 记录异常日志
                Console.WriteLine($"定时索引出错: {ex.Message}");
            }
        }
    }
}