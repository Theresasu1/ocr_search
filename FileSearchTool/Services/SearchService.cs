using FileSearchTool.Data;
using FileSearchTool.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ModelMatchType = FileSearchTool.Model.MatchType;

namespace FileSearchTool.Services
{
    public class SearchDebouncer
    {
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(300);

        public async Task<T> Debounce<T>(Func<Task<T>> action)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await Task.Delay(_debounceDelay, _cancellationTokenSource.Token);
                return await action();
            }
            catch (OperationCanceledException)
            {
                // 被取消，返回默认值
                return default(T);
            }
        }
    }

    public class SearchService
    {
        private readonly SearchDbContext _dbContext;
        private readonly SearchDebouncer _searchDebouncer;
        private readonly string _indexPath;

        public SearchService(SearchDbContext dbContext, string indexPath = "")
        {
            _dbContext = dbContext;
            _searchDebouncer = new SearchDebouncer();
            _indexPath = string.IsNullOrEmpty(indexPath) ? Environment.CurrentDirectory : indexPath;
        }

        /// <summary>
        /// 异步搜索方法
        /// </summary>
        /// <param name="searchText">搜索文本</param>
        /// <returns>搜索结果列表</returns>
        public async Task<List<SearchResult>> SearchAsync(string searchText)
        {
            return await Search(searchText, "");
        }

        public async Task<List<SearchResult>> SearchWithDebounce(string searchText, string fileType)
        {
            var results = await _searchDebouncer.Debounce(async () => await Search(searchText, fileType));
            return results ?? new List<SearchResult>();
        }

        public async Task<List<SearchResult>> Search(string searchText, string fileType)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return new List<SearchResult>();
            }

            // 检查搜索关键字长度
            var trimmedSearchText = searchText.Trim();
            if (trimmedSearchText.Length < 2)
            {
                return new List<SearchResult>();
            }

            try
            {
                Console.WriteLine($"开始搜索: {searchText}, 文件类型: {fileType}");

                var results = await PerformSearch(searchText, fileType);

                Console.WriteLine($"搜索完成，找到 {results.Count} 个结果");

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"搜索时发生错误: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> PerformSearch(string searchText, string fileType)
        {
            try
            {
                var allowedExtensions = GetAllowedExtensionsForFileType(fileType);

                // 使用 FTS5 进行全文搜索
                var results = new List<SearchResult>();
                var conn = _dbContext.Database.GetDbConnection();
                var shouldClose = conn.State != System.Data.ConnectionState.Open;
                if (shouldClose)
                {
                    await conn.OpenAsync();
                }
                try
                {
                    try
                    {
                        using var pragma = conn.CreateCommand();
                        pragma.CommandText = "PRAGMA busy_timeout=5000;";
                        await pragma.ExecuteNonQueryAsync();
                    }
                    catch { }
                    using var cmd = conn.CreateCommand();
                    // 设置查询超时，避免长时间卡死
                    cmd.CommandTimeout = 10; // 10秒超时

                    // 构造FTS查询（支持中文短语与多关键字）
                    var ftsQuery = BuildFtsQuery(searchText);

                    cmd.CommandText = @"
                    SELECT
                        f.Id,
                        f.Path as FilePath,
                        f.Name as FileName,
                        f.Size as FileSize,
                        f.ModifiedDate as LastModified,
                        ci.Content
                    FROM ContentIndexFts
                    JOIN Files f ON f.Path = ContentIndexFts.FilePath
                    LEFT JOIN ContentIndex ci ON ci.FileId = f.Id
                    WHERE ContentIndexFts MATCH @query
                    LIMIT 200;";

                    var p = cmd.CreateParameter();
                    p.ParameterName = "@query";
                    p.Value = ftsQuery;
                    cmd.Parameters.Add(p);

                    var searchStartTime = DateTime.Now;
                    Console.WriteLine($"FTS5查询: {ftsQuery}");

                    try
                    {
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            var filePath = reader["FilePath"]?.ToString() ?? string.Empty;
                            var fileName = reader["FileName"]?.ToString() ?? Path.GetFileName(filePath);
                            long fileSize = 0;
                            var fsObj = reader["FileSize"];
                            if (fsObj is long l) fileSize = l; else if (fsObj != null) fileSize = Convert.ToInt64(fsObj);
                            DateTime lastModified;
                            var lmObj = reader["LastModified"];
                            if (lmObj is DateTime dt) lastModified = dt;
                            else if (lmObj is string s && DateTime.TryParse(s, out var parsed)) lastModified = parsed;
                            else lastModified = DateTime.MinValue;

                            var compressedContent = reader["Content"]?.ToString();

                            float score = 0f;

                            // 提取内容片段
                            var snippet = ExtractContentSnippet(compressedContent, searchText, 200);

                            results.Add(new SearchResult
                            {
                                Id = (int)reader["Id"],
                                FilePath = filePath,
                                FileName = fileName,
                                FileSize = fileSize,
                                LastModified = lastModified,
                                Content = compressedContent,
                                Score = score,
                                MatchType = Model.MatchType.Index,
                                Snippet = snippet // 添加Snippet属性
                            });
                        }

                        var elapsed = (DateTime.Now - searchStartTime).TotalMilliseconds;
                        Console.WriteLine($"FTS5查询完成，耗时 {elapsed:F2}ms，找到 {results.Count} 个结果");
                    }
                    catch (Exception ftsEx)
                    {
                        Console.WriteLine($"FTS5查询失败: {ftsEx.Message}");
                    }
                }
                finally
                {
                    if (shouldClose)
                    {
                        await conn.CloseAsync();
                    }
                }

                // 设置匹配类型
                foreach (var result in results)
                {
                    result.MatchType = Model.MatchType.Index;
                }

                // 应用文件类型过滤
                if (allowedExtensions.Any())
                {
                    results = results.Where(r => allowedExtensions.Contains(System.IO.Path.GetExtension(r.FilePath).ToLowerInvariant())).ToList();
                }

                // 根据选择的索引路径过滤结果
                if (!string.IsNullOrWhiteSpace(_indexPath))
                {
                    // 处理多个路径（用分号分隔）
                    var paths = _indexPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();

                    if (paths.Any())
                    {
                        results = results.Where(r =>
                            paths.Any(p => r.FilePath.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                    }
                }

                // 优化：只在索引确实存在但无结果时才进行回退搜索
                if (!results.Any())
                {
                    Console.WriteLine("FTS 搜索无结果，尝试 LIKE 内容回退搜索（支持中文）");
                    // 使用 LIKE 查询作为回退方案，支持中文搜索
                    results = await PerformLikeContentSearch(searchText, allowedExtensions?.ToArray() ?? Array.Empty<string>());

                    // 如果 LIKE 内容搜索也无结果，再尝试文件名匹配
                    if (!results.Any())
                    {
                        Console.WriteLine("LIKE 内容搜索无结果，尝试文件名匹配");
                        results = await PerformFileNameSearch(searchText, allowedExtensions?.ToArray() ?? Array.Empty<string>());
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"执行搜索时发生错误: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        /// <summary>
        /// 获取文件内容
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件内容</returns>
        public async Task<string?> GetFileContentAsync(string filePath)
        {
            try
            {
                // 首先尝试从数据库中获取内容
                var fileEntity = await _dbContext.Files
                    .FirstOrDefaultAsync(f => f.Path == filePath);

                if (fileEntity != null)
                {
                    // 从ContentIndex表中获取内容
                    var contentIndex = await _dbContext.ContentIndex
                        .FirstOrDefaultAsync(ci => ci.FileId == fileEntity.Id);

                    if (contentIndex != null)
                    {
                        return contentIndex.Content;
                    }
                }

                // 如果数据库中没有内容，尝试直接从文件读取
                if (File.Exists(filePath))
                {
                    return await File.ReadAllTextAsync(filePath);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文件内容时出错 {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清理无效记录
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

        // 其他方法保持不变...
        private async Task<List<SearchResult>> PerformLikeContentSearch(string searchText, string[] allowedExtensions)
        {
            var results = new List<SearchResult>();
            try
            {
                var conn = _dbContext.Database.GetDbConnection();
                var shouldClose = conn.State != System.Data.ConnectionState.Open;
                if (shouldClose) await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandTimeout = 10;
                    cmd.CommandText = @"
                        SELECT f.Id, f.Path AS FilePath, f.Name AS FileName, f.Size AS FileSize, f.ModifiedDate AS LastModified, ci.Content
                        FROM ContentIndex ci
                        JOIN Files f ON ci.FileId = f.Id
                        WHERE ci.Content LIKE @pattern
                        LIMIT 200;";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "@pattern";
                    p.Value = "%" + searchText.Replace("%", "[%]") + "%";
                    cmd.Parameters.Add(p);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var filePath = reader["FilePath"]?.ToString() ?? string.Empty;
                        var fileName = reader["FileName"]?.ToString() ?? Path.GetFileName(filePath);
                        long fileSize = 0;
                        var fsObj = reader["FileSize"]; if (fsObj is long l) fileSize = l; else if (fsObj != null) fileSize = Convert.ToInt64(fsObj);
                        DateTime lastModified;
                        var lmObj = reader["LastModified"]; if (lmObj is DateTime dt) lastModified = dt; else if (lmObj is string s && DateTime.TryParse(s, out var parsed)) lastModified = parsed; else lastModified = DateTime.MinValue;
                        var content = reader["Content"]?.ToString();
                        var snippet = ExtractContentSnippet(content, searchText, 200);
                        results.Add(new SearchResult
                        {
                            Id = (int)reader["Id"],
                            FilePath = filePath,
                            FileName = fileName,
                            FileSize = fileSize,
                            LastModified = lastModified,
                            Content = content,
                            Score = 0f,
                            MatchType = Model.MatchType.Index,
                            Snippet = snippet
                        });
                    }
                }
                finally
                {
                    if (shouldClose) await conn.CloseAsync();
                }

                // 扩展过滤
                if (allowedExtensions != null && allowedExtensions.Length > 0)
                {
                    results = results.Where(r => allowedExtensions.Contains(System.IO.Path.GetExtension(r.FilePath).ToLowerInvariant())).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LIKE 内容搜索失败: {ex.Message}");
            }
            return results;
        }

        private async Task<List<SearchResult>> PerformFileNameSearch(string searchText, string[] allowedExtensions)
        {
            var results = new List<SearchResult>();
            try
            {
                var pattern = "%" + searchText.Replace("%", "[%]") + "%";
                var files = await _dbContext.Files
                    .Where(f => EF.Functions.Like(f.Name, pattern))
                    .Take(200)
                    .ToListAsync();

                foreach (var f in files)
                {
                    var contentIndex = await _dbContext.ContentIndex.FirstOrDefaultAsync(ci => ci.FileId == f.Id);
                    var snippet = ExtractContentSnippet(contentIndex?.Content, searchText, 200);
                    results.Add(new SearchResult
                    {
                        Id = f.Id,
                        FilePath = f.Path,
                        FileName = f.Name,
                        FileSize = f.Size,
                        LastModified = f.ModifiedDate,
                        Content = contentIndex?.Content,
                        Score = 0f,
                        MatchType = Model.MatchType.FileName,
                        Snippet = snippet
                    });
                }

                if (allowedExtensions != null && allowedExtensions.Length > 0)
                {
                    results = results.Where(r => allowedExtensions.Contains(System.IO.Path.GetExtension(r.FilePath).ToLowerInvariant())).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"文件名搜索失败: {ex.Message}");
            }
            return results;
        }

        private List<string> GetAllowedExtensionsForFileType(string fileType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fileType)) return new List<string>();
                var lower = fileType.ToLowerInvariant();
                // 简单映射：可根据需求扩展
                return lower switch
                {
                    "code" => new List<string>{".cs",".js",".ts",".java",".cpp",".h",".py",".go",".rs"},
                    "doc" => new List<string>{".doc",".docx",".pdf",".txt",".md"},
                    "sheet" => new List<string>{".xls",".xlsx",".csv"},
                    "slide" => new List<string>{".ppt",".pptx"},
                    _ => new List<string>()
                };
            }
            catch { return new List<string>(); }
        }

        private string BuildFtsQuery(string searchText)
        {
            var text = (searchText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text)) return "";
            // 将空白分隔的关键字转为 AND 查询；保留中文短语
            var parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1) return text.Replace("\"", "\"\"");
            var escaped = parts.Select(p => p.Replace("\"", "\"\""));
            return string.Join(" AND ", escaped);
        }

        private string ExtractContentSnippet(string? content, string searchText, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            var text = content;
            var idx = text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return text.Length > maxLength ? text.Substring(0, maxLength) : text;
            }
            var start = Math.Max(0, idx - maxLength / 2);
            var len = Math.Min(maxLength, text.Length - start);
            var snippet = text.Substring(start, len);
            return snippet;
        }
    }
}
