using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FileSearchTool.Data;
using Microsoft.EntityFrameworkCore;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 索引存储服务，负责文件索引的存储和检索
    /// </summary>
    public class IndexStorageService
    {
        private readonly SearchDbContext _dbContext;
        private readonly ContentExtractorService _contentExtractor;

        public IndexStorageService(SearchDbContext dbContext, ContentExtractorService contentExtractor)
        {
            _dbContext = dbContext;
            _contentExtractor = contentExtractor;
        }

        /// <summary>
        /// 索引文件内容（支持取消）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>索引是否成功</returns>
        public async Task<bool> IndexFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            try
            {
                // 检查取消请求
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    Console.WriteLine("文件路径为空，跳过索引");
                    return false;
                }

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"文件不存在，跳过索引: {filePath}");
                    return false;
                }

                // 提取文件内容
                var content = await _contentExtractor.ExtractTextAsync(filePath, cancellationToken);
        
                // 检查取消请求
                cancellationToken.ThrowIfCancellationRequested();

                // 获取文件信息
                var fileInfo = new FileInfo(filePath);
                var lastModified = fileInfo.LastWriteTimeUtc;

                // 更新或插入索引记录
                var fileRecord = await _dbContext.Files
                    .FirstOrDefaultAsync(f => f.Path == filePath, cancellationToken);

                if (fileRecord == null)
                {
                    fileRecord = new FileEntity
                    {
                        Path = filePath,
                        Name = fileInfo.Name,
                        Size = fileInfo.Length,
                        ModifiedDate = lastModified,
                        Hash = ComputeFileHash(filePath)
                    };
                    _dbContext.Files.Add(fileRecord);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    fileRecord.Size = fileInfo.Length;
                    fileRecord.ModifiedDate = lastModified;
                    fileRecord.Hash = ComputeFileHash(filePath);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                var contentIndex = await _dbContext.ContentIndex
                    .FirstOrDefaultAsync(ci => ci.FileId == fileRecord.Id, cancellationToken);

                // 限制内容长度以避免数据库字段超限
                var truncatedContent = content;
                if (!string.IsNullOrEmpty(truncatedContent) && truncatedContent.Length > 1000000) // 限制为100万字符
                {
                    truncatedContent = truncatedContent.Substring(0, 1000000);
                    Console.WriteLine($"内容已截断至100万字符: {filePath}");
                }

                if (contentIndex == null)
                {
                    contentIndex = new ContentIndexEntity
                    {
                        FileId = fileRecord.Id,
                        Content = truncatedContent,
                        IndexedDate = DateTime.UtcNow
                    };
                    _dbContext.ContentIndex.Add(contentIndex);
                }
                else
                {
                    contentIndex.Content = truncatedContent;
                    contentIndex.IndexedDate = DateTime.UtcNow;
                }

                // 在保存更改之前，检查实体状态
                var entries = _dbContext.ChangeTracker.Entries();
                foreach (var entry in entries)
                {
                    if (entry.State == EntityState.Unchanged)
                    {
                        Console.WriteLine($"警告: 发现未更改的实体 {entry.Entity.GetType().Name}");
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
        
                Console.WriteLine($"成功索引文件: {filePath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                // 取消操作，重新抛出异常
                Console.WriteLine($"索引操作被取消: {filePath}");
                throw;
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                // 特别处理参数超出范围的异常
                Console.WriteLine($"索引文件时参数超出范围 {filePath}: {argEx.Message}");
                Console.WriteLine($"参数名称: {argEx.ParamName}");
                Console.WriteLine($"实际值: {argEx.ActualValue}");
                Console.WriteLine($"堆栈跟踪: {argEx.StackTrace}");
                return false;
            }
            catch (DbUpdateException dbEx)
            {
                // 记录数据库更新异常的详细信息
                Console.WriteLine($"索引文件时数据库更新出错 {filePath}: {dbEx.Message}");
                Console.WriteLine($"内部异常: {dbEx.InnerException?.Message}");
                Console.WriteLine($"堆栈跟踪: {dbEx.StackTrace}");
                
                // 尝试获取更多关于哪个实体导致问题的信息
                if (dbEx.Entries != null)
                {
                    foreach (var entry in dbEx.Entries)
                    {
                        Console.WriteLine($"实体类型: {entry.Entity.GetType().Name}");
                        Console.WriteLine($"实体状态: {entry.State}");
                        // 如果实体状态是Unchanged，尝试将其标记为Modified
                        if (entry.State == EntityState.Unchanged)
                        {
                            Console.WriteLine($"尝试将实体状态从Unchanged更改为Modified");
                            entry.State = EntityState.Modified;
                        }
                    }
                    
                    // 尝试再次保存更改
                    try
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        Console.WriteLine($"重新保存更改成功: {filePath}");
                        return true;
                    }
                    catch (Exception retryEx)
                    {
                        Console.WriteLine($"重新保存更改失败: {retryEx.Message}");
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                // 记录详细的错误信息
                Console.WriteLine($"索引文件时出错 {filePath}: {ex.Message}");
                Console.WriteLine($"详细错误信息: {ex}");
                return false;
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using var fs = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// 从索引中移除文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否成功移除</returns>
        public async Task<bool> RemoveFileFromIndexAsync(string filePath)
        {
            try
            {
                var record = await _dbContext.Files
                    .FirstOrDefaultAsync(f => f.Path == filePath);

                if (record != null)
                {
                    _dbContext.Files.Remove(record);
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"成功从索引中移除文件: {filePath}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"从索引中移除文件时出错 {filePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理无效记录（文件已不存在的记录）
        /// </summary>
        /// <returns>清理的记录数</returns>
        public async Task<int> CleanupInvalidRecordsAsync()
        {
            try
            {
                var invalidRecords = await _dbContext.Files
                    .Where(f => !File.Exists(f.Path))
                    .ToListAsync();

                if (invalidRecords.Any())
                {
                    _dbContext.Files.RemoveRange(invalidRecords);
                    await _dbContext.SaveChangesAsync();
                    Console.WriteLine($"清理了 {invalidRecords.Count} 条无效记录");
                    return invalidRecords.Count;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清理无效记录时出错: {ex.Message}");
                return 0;
            }
        }
        
        /// <summary>
        /// 清空数据库
        /// </summary>
        /// <returns>任务</returns>
        public async Task ClearDatabaseAsync()
        {
            try
            {
                _dbContext.ContentIndex.RemoveRange(_dbContext.ContentIndex);
                _dbContext.Files.RemoveRange(_dbContext.Files);
                await _dbContext.SaveChangesAsync();
                Console.WriteLine("数据库已清空");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清空数据库时出错: {ex.Message}");
                throw;
            }
        }
    }
}