using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 扫描进度信息
    /// </summary>
    public class ScanProgress
    {
        public int ProcessedDirectories { get; set; }
        public int TotalDirectories { get; set; }
        public string CurrentDirectory { get; set; } = string.Empty;
    }

    /// <summary>
    /// 文件扫描服务
    /// </summary>
    public class FileScannerService
    {
        private readonly List<string> _includedExtensions;
        private readonly List<string> _excludedExtensions;
        private readonly List<string> _excludedDirectories;
        private long _maxFileSize; // 移除readonly修饰符

        public FileScannerService()
        {
            _includedExtensions = new List<string>();
            _excludedExtensions = new List<string> { ".tmp", ".log", ".cache" };
            _excludedDirectories = new List<string> { "node_modules", "bin", "obj", ".git", ".vs" };
            _maxFileSize = 100 * 1024 * 1024; // 100MB
        }

        /// <summary>
        /// 并行扫描指定目录中的文件
        /// </summary>
        /// <param name="directoryPath">要扫描的目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progress">进度报告器</param>
        /// <param name="lastIndexedTime">上次索引时间，用于增量索引</param>
        /// <returns>扫描到的文件列表</returns>
        public async Task<List<FileInfo>> ScanDirectoryParallelAsync(
            string directoryPath,
            CancellationToken cancellationToken = default,
            IProgress<ScanProgress>? progress = null,
            DateTime? lastIndexedTime = null)
        {
            var files = new List<FileInfo>();
            var directories = new Queue<string>();
            var processedDirectories = 0;
            var totalDirectories = 1; // 至少有一个根目录

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"目录不存在: {directoryPath}");
                return files;
            }

            directories.Enqueue(directoryPath);

            // 使用并发度限制的并行处理
            var maxDegreeOfParallelism = Environment.ProcessorCount;
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism, maxDegreeOfParallelism);

            var tasks = new List<Task>();

            while (directories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentDirectory = directories.Dequeue();
                processedDirectories++;

                // 报告进度
                progress?.Report(new ScanProgress
                {
                    ProcessedDirectories = processedDirectories,
                    TotalDirectories = totalDirectories,
                    CurrentDirectory = currentDirectory
                });

                try
                {
                    // 检查是否应该排除此目录
                    if (ShouldExcludeDirectory(currentDirectory))
                    {
                        continue;
                    }

                    // 获取子目录
                    var subDirectories = Directory.GetDirectories(currentDirectory);
                    foreach (var subDir in subDirectories)
                    {
                        if (!ShouldExcludeDirectory(subDir))
                        {
                            directories.Enqueue(subDir);
                            totalDirectories++;
                        }
                    }

                    // 创建处理当前目录文件的任务
                    var task = Task.Run(async () =>
                    {
                        await semaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var dirFiles = GetFilesInDirectory(currentDirectory, lastIndexedTime);
                            lock (files)
                            {
                                files.AddRange(dirFiles);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken);

                    tasks.Add(task);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"扫描目录时出错 {currentDirectory}: {ex.Message}");
                }
            }

            // 等待所有任务完成
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // 重新抛出取消异常
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"并行扫描过程中出错: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// 获取目录中的文件（根据过滤条件）
        /// </summary>
        private List<FileInfo> GetFilesInDirectory(string directoryPath, DateTime? lastIndexedTime)
        {
            var files = new List<FileInfo>();

            try
            {
                var directoryInfo = new DirectoryInfo(directoryPath);
                var fileInfos = directoryInfo.GetFiles();

                foreach (var fileInfo in fileInfos)
                {
                    // 检查文件是否应该被包含
                    if (ShouldIncludeFile(fileInfo, lastIndexedTime))
                    {
                        files.Add(fileInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取目录文件时出错 {directoryPath}: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// 判断是否应该包含文件
        /// </summary>
        private bool ShouldIncludeFile(FileInfo fileInfo, DateTime? lastIndexedTime)
        {
            // 检查文件大小
            if (fileInfo.Length > _maxFileSize)
            {
                return false;
            }

            // 检查扩展名
            var extension = fileInfo.Extension.ToLowerInvariant();
            
            // 如果指定了包含的扩展名列表，只包含列表中的文件
            if (_includedExtensions.Any() && !_includedExtensions.Contains(extension))
            {
                return false;
            }

            // 检查排除的扩展名
            if (_excludedExtensions.Contains(extension))
            {
                return false;
            }

            // 如果指定了上次索引时间，只包含在此之后修改的文件
            if (lastIndexedTime.HasValue && fileInfo.LastWriteTimeUtc <= lastIndexedTime.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判断是否应该排除目录
        /// </summary>
        private bool ShouldExcludeDirectory(string directoryPath)
        {
            var dirName = Path.GetFileName(directoryPath);
            return _excludedDirectories.Contains(dirName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 设置包含的文件扩展名
        /// </summary>
        public void SetIncludedExtensions(IEnumerable<string> extensions)
        {
            _includedExtensions.Clear();
            _includedExtensions.AddRange(extensions.Select(e => e.ToLowerInvariant()));
        }

        /// <summary>
        /// 设置排除的文件扩展名
        /// </summary>
        public void SetExcludedExtensions(IEnumerable<string> extensions)
        {
            _excludedExtensions.Clear();
            _excludedExtensions.AddRange(extensions.Select(e => e.ToLowerInvariant()));
        }

        /// <summary>
        /// 设置排除的目录名
        /// </summary>
        public void SetExcludedDirectories(IEnumerable<string> directories)
        {
            _excludedDirectories.Clear();
            _excludedDirectories.AddRange(directories);
        }

        /// <summary>
        /// 设置最大文件大小限制
        /// </summary>
        public void SetMaxFileSize(long maxFileSize)
        {
            _maxFileSize = maxFileSize;
        }
    }
}