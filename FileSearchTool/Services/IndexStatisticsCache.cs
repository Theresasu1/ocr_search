using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FileSearchTool.Data;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 索引统计信息缓存服务
    /// </summary>
    public class IndexStatisticsCache
    {
        private static IndexStatisticsCache? _instance;
        private static readonly object _lock = new object();
        
        private IndexStatistics? _cachedStatistics;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5); // 缓存有效期5分钟
        private bool _isLoading = false;
        
        public static IndexStatisticsCache Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new IndexStatisticsCache();
                        }
                    }
                }
                return _instance;
            }
        }
        
        private IndexStatisticsCache()
        {
            // 私有构造函数，确保单例
        }
        
        /// <summary>
        /// 获取统计信息（带缓存）
        /// </summary>
        public async Task<IndexStatistics> GetStatisticsAsync(bool forceRefresh = false)
        {
            // 如果缓存有效且不强制刷新，直接返回缓存
            if (!forceRefresh && _cachedStatistics != null && 
                DateTime.Now - _lastUpdateTime < _cacheExpiration)
            {
                Console.WriteLine("使用缓存数据，立即返回");
                return _cachedStatistics;
            }
            
            // 如果正在加载，等待加载完成
            if (_isLoading)
            {
                Console.WriteLine("正在加载中，等待...");
                // 简单等待策略，最多等待3秒
                int waitCount = 0;
                while (_isLoading && waitCount < 30)
                {
                    await Task.Delay(100);
                    waitCount++;
                }
                
                // 如果加载完成，返回缓存
                if (_cachedStatistics != null)
                {
                    Console.WriteLine("等待后获取到缓存数据");
                    return _cachedStatistics;
                }
            }
            
            // 开始加载
            Console.WriteLine("开始加载索引统计信息...");
            _isLoading = true;
            
            try
            {
                var statistics = await LoadStatisticsAsync();
                _cachedStatistics = statistics;
                _lastUpdateTime = DateTime.Now;
                Console.WriteLine("索引统计信息加载完成");
                return statistics;
            }
            finally
            {
                _isLoading = false;
            }
        }
        
        /// <summary>
        /// 同步获取缓存数据（仅返回已缓存的数据，不等待加载）
        /// </summary>
        public IndexStatistics? GetCachedStatisticsSync()
        {
            if (_cachedStatistics != null && 
                DateTime.Now - _lastUpdateTime < _cacheExpiration)
            {
                return _cachedStatistics;
            }
            return null;
        }
        
        /// <summary>
        /// 实际加载统计信息
        /// </summary>
        private async Task<IndexStatistics> LoadStatisticsAsync()
        {
            return await Task.Run(async () =>
            {
                var statistics = new IndexStatistics();
                var startTime = DateTime.Now;
                
                try
                {
                    // 获取数据库路径（使用配置服务）
                    var dbPath = DatabaseConfigService.GetDatabaseFilePath();
                    statistics.DatabasePath = dbPath;
                    
                    // 获取数据库文件大小（快速文件操作）
                    if (File.Exists(dbPath))
                    {
                        var fileInfo = new FileInfo(dbPath);
                        statistics.DatabaseSizeBytes = fileInfo.Length;
                        statistics.DatabaseSizeMB = fileInfo.Length / (1024.0 * 1024.0);
                    }
                    
                    // 使用专用的轻量级查询，避免慢查询
                    using var dbContext = new SearchDbContext();
                    var conn = dbContext.Database.GetDbConnection();
                    await conn.OpenAsync();
                    
                    try
                    {
                        // 使用原生SQL快速查询文件数量（避免EF Core开销）
                        using var countCmd = conn.CreateCommand();
                        countCmd.CommandText = "SELECT COUNT(*) FROM Files";
                        var countResult = await countCmd.ExecuteScalarAsync();
                        statistics.FileCount = Convert.ToInt32(countResult);
                        
                        Console.WriteLine($"文件数量查询完成: {statistics.FileCount} 个文件");
                        
                        // 只在有文件时查询最后索引时间
                        if (statistics.FileCount > 0)
                        {
                            using var timeCmd = conn.CreateCommand();
                            timeCmd.CommandText = "SELECT MAX(IndexedDate) FROM ContentIndex";
                            var timeResult = await timeCmd.ExecuteScalarAsync();
                            
                            if (timeResult != null && timeResult != DBNull.Value)
                            {
                                if (timeResult is DateTime dt)
                                {
                                    statistics.LastIndexTime = dt;
                                }
                                else if (DateTime.TryParse(timeResult.ToString(), out var parsed))
                                {
                                    statistics.LastIndexTime = parsed;
                                }
                            }
                            
                            Console.WriteLine($"最后索引时间查询完成: {statistics.LastIndexTime}");
                        }
                    }
                    finally
                    {
                        await conn.CloseAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载索引统计信息失败: {ex.Message}");
                    statistics.HasError = true;
                    statistics.ErrorMessage = ex.Message;
                }
                
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"索引统计信息加载总耗时: {elapsed:F2}ms");
                
                return statistics;
            });
        }
        
        /// <summary>
        /// 预加载统计信息（后台静默加载）
        /// </summary>
        public async Task PreloadAsync()
        {
            try
            {
                await GetStatisticsAsync(forceRefresh: false);
                Console.WriteLine("索引统计信息预加载完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"预加载索引统计信息失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 使缓存失效（索引更新后调用）
        /// </summary>
        public void InvalidateCache()
        {
            _cachedStatistics = null;
            _lastUpdateTime = DateTime.MinValue;
            Console.WriteLine("索引统计缓存已失效");
        }
        
        /// <summary>
        /// 增量更新缓存（索引新增/删除后调用）
        /// </summary>
        public void UpdateCache(int fileCountDelta)
        {
            if (_cachedStatistics != null)
            {
                _cachedStatistics.FileCount += fileCountDelta;
                _cachedStatistics.LastIndexTime = DateTime.Now;
                _lastUpdateTime = DateTime.Now;
                Console.WriteLine($"索引统计缓存已更新: 文件数变化 {fileCountDelta}");
            }
        }
    }
    
    /// <summary>
    /// 索引统计信息数据类
    /// </summary>
    public class IndexStatistics
    {
        public int FileCount { get; set; }
        public long DatabaseSizeBytes { get; set; }
        public double DatabaseSizeMB { get; set; }
        public DateTime LastIndexTime { get; set; } = DateTime.MinValue;
        public string DatabasePath { get; set; } = string.Empty;
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
