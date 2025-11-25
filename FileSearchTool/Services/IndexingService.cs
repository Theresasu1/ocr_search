using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FileSearchTool.Data;
using Microsoft.EntityFrameworkCore;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 索引进度信息
    /// </summary>
    public class IndexingProgress
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public int ProgressPercentage { get; set; }
        public string Status { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
    }

    /// <summary>
    /// 索引服务
    /// </summary>
    public class IndexingService
    {
        private readonly SearchDbContext _dbContext;
        private readonly ContentExtractorService _contentExtractor;
        private readonly FileScannerService _fileScanner;
        private readonly IndexStorageService _indexStorage;
        
        // 添加字段来存储设置
        private IEnumerable<string> _allowedExtensions = new List<string>();
        private bool _indexAllFiles = true;
        private long _maxFileSize = long.MaxValue;
        private IEnumerable<string> _excludedSubdirectories = new List<string>();
        private int _indexingCores = Environment.ProcessorCount;

        public IndexingService(SearchDbContext dbContext, ContentExtractorService contentExtractor, FileScannerService fileScanner)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _contentExtractor = contentExtractor ?? throw new ArgumentNullException(nameof(contentExtractor));
            _fileScanner = fileScanner ?? throw new ArgumentNullException(nameof(fileScanner));
            _indexStorage = new IndexStorageService(_dbContext, _contentExtractor);
        }

        // 新增：设置文件类型白名单
        public void SetAllowedExtensions(IEnumerable<string> extensions)
        {
            _allowedExtensions = extensions ?? new List<string>();
        }

        // 新增：设置是否索引所有文件
        public void SetIndexAllFiles(bool indexAll)
        {
            _indexAllFiles = indexAll;
        }

        // 新增：获取当前文件类型白名单
        public IEnumerable<string> GetAllowedExtensions()
        {
            return _allowedExtensions;
        }

        // 新增：获取当前是否索引所有文件设置
        public bool GetIndexAllFiles()
        {
            return _indexAllFiles;
        }

        // 新增：最大文件大小设置
        public void SetMaxFileSize(long maxSize)
        {
            _maxFileSize = maxSize;
        }

        public long GetMaxFileSize()
        {
            return _maxFileSize;
        }

        // 新增：排除指定目录设置
        public void SetExcludedSubdirectories(IEnumerable<string> paths)
        {
            _excludedSubdirectories = paths ?? new List<string>();
        }
        
        // 新增：获取 IndexStorage 实例
        public IndexStorageService GetIndexStorage()
        {
            return _indexStorage;
        }
        
        // 新增：设置CPU核心数
        public void SetIndexingCores(int cores)
        {
            _indexingCores = cores > 0 ? cores : Environment.ProcessorCount;
        }
        
        // 新增：获取CPU核心数
        public int GetIndexingCores()
        {
            return _indexingCores;
        }

        /// <summary>
        /// 开始索引指定目录
        /// </summary>
        /// <param name="directoryPath">要索引的目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progress">进度报告器</param>
        /// <returns>索引任务</returns>
        public async Task StartIndexingAsync(
            string directoryPath,
            CancellationToken cancellationToken = default,
            IProgress<IndexingProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("目录路径不能为空", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");

            try
            {
                // 通知开始扫描
                progress?.Report(new IndexingProgress
                {
                    Status = "正在扫描目录...",
                    ProcessedFiles = 0,
                    TotalFiles = 0
                });

                // 扫描目录中的文件
                var files = await _fileScanner.ScanDirectoryParallelAsync(
                    directoryPath,
                    cancellationToken,
                    null); // 不需要扫描进度报告

                // 通知扫描完成
                progress?.Report(new IndexingProgress
                {
                    Status = "扫描完成，开始索引文件...",
                    ProcessedFiles = 0,
                    TotalFiles = files.Count
                });

                // 索引文件
                await IndexFilesAsync(files, cancellationToken, progress);
            }
            catch (OperationCanceledException)
            {
                // 重新抛出取消异常
                throw;
            }
            catch (Exception ex)
            {
                // 记录错误并重新抛出
                Debug.WriteLine($"索引过程中发生错误: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 索引文件列表
        /// </summary>
        private async Task IndexFilesAsync(
            List<FileInfo> files,
            CancellationToken cancellationToken,
            IProgress<IndexingProgress>? progress)
        {
            int processedFiles = 0;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 批量处理文件以提高性能
                const int batchSize = 50;
                for (int i = 0; i < files.Count; i += batchSize)
                {
                    // 检查是否需要取消
                    cancellationToken.ThrowIfCancellationRequested();

                    // 获取当前批次
                    var batch = files.Skip(i).Take(batchSize).ToList();

                    // 处理批次中的文件
                    foreach (var file in batch)
                    {
                        // 检查是否需要取消
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            // 更新进度
                            processedFiles++;
                            progress?.Report(new IndexingProgress
                            {
                                Status = "正在索引文件...",
                                ProcessedFiles = processedFiles,
                                TotalFiles = files.Count,
                                CurrentFile = file.FullName
                            });

                            // 索引单个文件
                            await _indexStorage.IndexFileAsync(file.FullName, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // 重新抛出取消异常
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // 记录单个文件的错误但继续处理其他文件
                            Debug.WriteLine($"索引文件 {file.FullName} 时出错: {ex.Message}");
                            // 可以选择在这里通知用户单个文件的错误
                        }
                    }

                    // 批量提交到数据库，使用独立的上下文避免并发问题
                    using (var newContext = new SearchDbContext())
                    {
                        await newContext.Database.OpenConnectionAsync(cancellationToken);
                        try
                        {
                            await newContext.SaveChangesAsync(cancellationToken);
                        }
                        finally
                        {
                            await newContext.Database.CloseConnectionAsync();
                        }
                    }
                }

                stopwatch.Stop();
                Debug.WriteLine($"索引完成，共处理 {files.Count} 个文件，耗时 {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (OperationCanceledException)
            {
                // 清理资源并重新抛出
                stopwatch.Stop();
                Debug.WriteLine("索引操作已被用户取消");
                throw;
            }
            catch (Exception ex)
            {
                // 记录错误并重新抛出
                stopwatch.Stop();
                Debug.WriteLine($"索引文件时发生错误: {ex}");
                throw;
            }
        }
        
        /// <summary>
        /// 索引指定目录（支持并发控制和进度报告）
        /// </summary>
        /// <param name="directoryPath">要索引的目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progress">进度报告器</param>
        /// <param name="maxConcurrentTasks">最大并发任务数</param>
        /// <param name="chunkSize">文件分块大小</param>
        /// <returns>索引任务</returns>
        public async Task IndexDirectoryAsync(
            string directoryPath,
            CancellationToken cancellationToken = default,
            IProgress<IndexingProgress>? progress = null,
            int maxConcurrentTasks = 4,
            int chunkSize = 100)
        {
            // 检查是否是多个路径（用分号分隔）
            if (!string.IsNullOrWhiteSpace(directoryPath) && directoryPath.Contains(";"))
            {
                await IndexMultipleDirectoriesAsync(
                    directoryPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList(),
                    cancellationToken,
                    progress,
                    maxConcurrentTasks,
                    chunkSize);
                return;
            }

            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("目录路径不能为空", nameof(directoryPath));

            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");

            try
            {
                // 通知开始扫描
                progress?.Report(new IndexingProgress
                {
                    Status = "正在扫描目录...",
                    ProcessedFiles = 0,
                    TotalFiles = 0
                });

                // 扫描目录中的文件
                var files = await _fileScanner.ScanDirectoryParallelAsync(
                    directoryPath,
                    cancellationToken,
                    null); // 不需要扫描进度报告

                // 通知扫描完成
                progress?.Report(new IndexingProgress
                {
                    Status = $"扫描完成，找到 {files.Count} 个文件，开始索引...",
                    ProcessedFiles = 0,
                    TotalFiles = files.Count
                });

                // 使用并发控制索引文件
                await IndexFilesWithConcurrencyControlAsync(
                    files, 
                    cancellationToken, 
                    progress, 
                    maxConcurrentTasks, 
                    chunkSize);
            }
            catch (OperationCanceledException)
            {
                // 重新抛出取消异常
                throw;
            }
            catch (Exception ex)
            {
                // 记录错误并重新抛出
                Debug.WriteLine($"索引过程中发生错误: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 索引多个目录（支持并发控制和进度报告）
        /// </summary>
        /// <param name="directoryPaths">要索引的目录路径列表</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progress">进度报告器</param>
        /// <param name="maxConcurrentTasks">最大并发任务数</param>
        /// <param name="chunkSize">文件分块大小</param>
        /// <returns>索引任务</returns>
        public async Task IndexMultipleDirectoriesAsync(
            List<string> directoryPaths,
            CancellationToken cancellationToken = default,
            IProgress<IndexingProgress>? progress = null,
            int maxConcurrentTasks = 4,
            int chunkSize = 100)
        {
            if (directoryPaths == null || directoryPaths.Count == 0)
                throw new ArgumentException("目录路径列表不能为空", nameof(directoryPaths));

            // 验证所有路径都存在
            foreach (var path in directoryPaths)
            {
                if (!Directory.Exists(path))
                    throw new DirectoryNotFoundException($"目录不存在: {path}");
            }

            try
            {
                // 通知开始扫描
                progress?.Report(new IndexingProgress
                {
                    Status = "正在扫描目录...",
                    ProcessedFiles = 0,
                    TotalFiles = 0
                });

                // 扫描所有目录中的文件
                var allFiles = new List<FileInfo>();
                foreach (var directoryPath in directoryPaths)
                {
                    var files = await _fileScanner.ScanDirectoryParallelAsync(
                        directoryPath,
                        cancellationToken,
                        null); // 不需要扫描进度报告
                    allFiles.AddRange(files);
                }

                // 通知扫描完成
                progress?.Report(new IndexingProgress
                {
                    Status = $"扫描完成，找到 {allFiles.Count} 个文件，开始索引...",
                    ProcessedFiles = 0,
                    TotalFiles = allFiles.Count
                });

                // 使用并发控制索引文件
                await IndexFilesWithConcurrencyControlAsync(
                    allFiles, 
                    cancellationToken, 
                    progress, 
                    maxConcurrentTasks, 
                    chunkSize);
            }
            catch (OperationCanceledException)
            {
                // 重新抛出取消异常
                throw;
            }
            catch (Exception ex)
            {
                // 记录错误并重新抛出
                Debug.WriteLine($"索引过程中发生错误: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 使用并发控制索引文件
        /// </summary>
        private async Task IndexFilesWithConcurrencyControlAsync(
            List<FileInfo> files,
            CancellationToken cancellationToken,
            IProgress<IndexingProgress>? progress,
            int maxConcurrentTasks,
            int chunkSize)
        {
            var processedFiles = 0;
            var semaphore = new SemaphoreSlim(maxConcurrentTasks, maxConcurrentTasks);
            var tasks = new List<Task>();

            // 将文件分块处理
            for (int i = 0; i < files.Count; i += chunkSize)
            {
                var chunk = files.Skip(i).Take(chunkSize).ToList();
                
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        // 为每个任务创建独立的DbContext实例以避免并发问题
                        using (var taskDbContext = new SearchDbContext())
                        {
                            var taskContentExtractor = new ContentExtractorService();
                            var taskIndexStorage = new IndexStorageService(taskDbContext, taskContentExtractor);
                            
                            foreach (var file in chunk)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                // 更新进度
                                Interlocked.Increment(ref processedFiles);
                                var currentProcessed = processedFiles;
                                
                                progress?.Report(new IndexingProgress
                                {
                                    Status = "正在索引文件...",
                                    ProcessedFiles = currentProcessed,
                                    TotalFiles = files.Count,
                                    CurrentFile = file.FullName
                                });

                                // 索引单个文件
                                await taskIndexStorage.IndexFileAsync(file.FullName, cancellationToken);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            // 等待所有任务完成
            await Task.WhenAll(tasks);
            
            progress?.Report(new IndexingProgress
            {
                Status = "索引完成",
                ProcessedFiles = files.Count,
                TotalFiles = files.Count,
                ProgressPercentage = 100
            });
        }
    }
}