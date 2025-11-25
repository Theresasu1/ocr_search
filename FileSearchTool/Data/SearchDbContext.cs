using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using FileSearchTool.Services;

namespace FileSearchTool.Data
{
    /// <summary>
    /// 搜索数据库上下文
    /// </summary>
    public class SearchDbContext : DbContext
    {
        public DbSet<FileEntity> Files { get; set; } = null!;
        public DbSet<ContentIndexEntity> ContentIndex { get; set; } = null!;
        public DbSet<FileExtensionEntity> FileExtensions { get; set; } = null!;

        private readonly string _databasePath;

        public SearchDbContext()
        {
            // 使用配置的数据库路径
            var directory = DatabaseConfigService.GetDatabaseDirectory();
            
            // 确保目录存在
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            _databasePath = Path.Combine(directory, "search_index.db");
        }
        
        public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options)
        {
            // 使用配置的数据库路径
            var directory = DatabaseConfigService.GetDatabaseDirectory();
            
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            _databasePath = Path.Combine(directory, "search_index.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 当使用 DbContext 池化时，不能在 OnConfiguring 中修改选项
            // 只有在完全未配置时才设置（非池化场景）
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = _databasePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared, // 使用共享缓存提高并发性能
                    Pooling = true // 启用连接池
                }.ToString();

                optionsBuilder.UseSqlite(connectionString);
            }
            // 如果已配置（池化场景），则跳过
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置Files表
            modelBuilder.Entity<FileEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Path).IsRequired();
                entity.Property(e => e.Name).IsRequired();
                entity.Property(e => e.Hash).IsRequired();
                entity.HasIndex(e => e.Path).IsUnique();
            });

            // 配置ContentIndex表（使用FTS5全文搜索）
            modelBuilder.Entity<ContentIndexEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(1000000); // 限制内容字段最大长度为100万字符
                // 注意：EF Core不直接支持FTS5，这里只是定义实体
            });

            // 配置FileExtensions表
            modelBuilder.Entity<FileExtensionEntity>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Extension).IsRequired();
                entity.HasIndex(e => e.Extension).IsUnique();
            });

            base.OnModelCreating(modelBuilder);
        }

        /// <summary>
        /// 初始化数据库，创建表和FTS5虚拟表
        /// </summary>
        public async Task InitializeAsync()
        {
            // 确保数据库创建
            await Database.EnsureCreatedAsync();

            // 优化 SQLite 并发与写入性能（WAL、busy_timeout、同步等级）
            try
            {
                await Database.OpenConnectionAsync();
                try
                {
                    await Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
                    await Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;");
                    await Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;");
                    await Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;");
                    // 追加：内存映射与页大小优化（根据通用 SSD 默认）
                    await Database.ExecuteSqlRawAsync("PRAGMA mmap_size=268435456;"); // 256MB
                    await Database.ExecuteSqlRawAsync("PRAGMA page_size=8192;");
                    // 添加查询优化
                    await Database.ExecuteSqlRawAsync("PRAGMA cache_size=-64000;"); // 64MB缓存
                    await Database.ExecuteSqlRawAsync("PRAGMA optimize;"); // 优化查询计划
                }
                finally
                {
                    await Database.CloseConnectionAsync();
                }
            }
            catch { /* 忽略 PRAGMA 设置错误 */ }

            // 创建FTS5全文索引表
            await CreateFtsTableAsync();
            
            // 初始化默认的文件扩展名
            await InitializeDefaultExtensionsAsync();
        }

        /// <summary>
        /// 创建FTS5全文索引表
        /// </summary>
        private async Task CreateFtsTableAsync()
        {
            try
            {
                // 检查 FTS5 表是否已存在
                using (var conn = Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ContentIndexFts'";
                    var name = await cmd.ExecuteScalarAsync();
                    
                    if (name != null)
                    {
                        // FTS表已存在，检查数据量
                        cmd.CommandText = "SELECT COUNT(*) FROM ContentIndexFts";
                        var countObj = await cmd.ExecuteScalarAsync();
                        var ftsCount = 0;
                        if (countObj is long l) ftsCount = (int)l; 
                        else if (countObj != null && int.TryParse(countObj.ToString(), out var c)) ftsCount = c;
                        
                        WriteLog($"FTS5表已存在，当前行数: {ftsCount}，跳过初始化");
                        return; // 跳过重复初始化
                    }
                }

                WriteLog("开始创建FTS5表...");
                
                // 创建新的 FTS5 表 - 使用简单的 unicode61 分词器
                var createFtsTableSql = @"
                    CREATE VIRTUAL TABLE IF NOT EXISTS ContentIndexFts USING fts5(
                        Content,
                        FilePath,
                        tokenize='unicode61'
                    );";

                await Database.ExecuteSqlRawAsync(createFtsTableSql);
                WriteLog("FTS5 表创建语句已执行");

                // 验证 FTS5 表是否存在
                using (var conn = Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='ContentIndexFts'";
                    var name = await cmd.ExecuteScalarAsync();
                    WriteLog(name != null ? "验证通过：ContentIndexFts 表存在" : "验证失败：ContentIndexFts 表不存在");
                }

                // 从 ContentIndex 表同步数据到 FTS5 表（使用事务批量插入优化性能）
                using (var conn = Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                                    
                    // 开启事务以提高批量插入性能
                    using var transaction = conn.BeginTransaction();
                                    
                    try
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = "SELECT ci.Content, f.Path FROM ContentIndex ci JOIN Files f ON ci.FileId = f.Id";
                        using var reader = await cmd.ExecuteReaderAsync();
                        int inserted = 0;
                                        
                        // 预编译插入语句以提高性能
                        using var insertCmd = conn.CreateCommand();
                        insertCmd.Transaction = transaction;
                        insertCmd.CommandText = "INSERT INTO ContentIndexFts (Content, FilePath) VALUES (@c, @p)";
                        var contentParam = insertCmd.CreateParameter();
                        contentParam.ParameterName = "@c";
                        insertCmd.Parameters.Add(contentParam);
                        var pathParam = insertCmd.CreateParameter();
                        pathParam.ParameterName = "@p";
                        insertCmd.Parameters.Add(pathParam);
                                        
                        while (await reader.ReadAsync())
                        {
                            var stored = reader.GetString(0);
                            var filePath = reader.GetString(1);
                            string plain;
                            if (!string.IsNullOrEmpty(stored) && stored.StartsWith("GZ:"))
                            {
                                try
                                {
                                    var b64 = stored.Substring(3);
                                    var bytes = Convert.FromBase64String(b64);
                                    using var ms = new System.IO.MemoryStream(bytes);
                                    using var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                                    using var sr = new System.IO.StreamReader(gzip, System.Text.Encoding.UTF8);
                                    plain = sr.ReadToEnd();
                                }
                                catch
                                {
                                    plain = stored; // 兖底
                                }
                            }
                            else
                            {
                                plain = stored;
                            }
                            // 截断后写入 FTS（与索引一致，最大 500_000 字符）
                            const int MaxChars = 500_000;
                            if (!string.IsNullOrEmpty(plain) && plain.Length > MaxChars)
                            {
                                plain = plain.Substring(0, MaxChars);
                            }
                                            
                            // 重用预编译语句，只更新参数值
                            contentParam.Value = plain;
                            pathParam.Value = filePath;
                            await insertCmd.ExecuteNonQueryAsync();
                            inserted++;
                        }
                                        
                        // 提交事务
                        transaction.Commit();
                        WriteLog($"已从 ContentIndex 同步数据到 ContentIndexFts（批量插入），计数: {inserted}");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        WriteLog($"同步数据失败: {ex.Message}");
                        throw;
                    }
                }
                WriteLog("已从 ContentIndex 同步数据到 ContentIndexFts");

                // 统计行数
                using (var conn = Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM ContentIndexFts";
                    var countObj = await cmd.ExecuteScalarAsync();
                    var count = 0;
                    if (countObj is long l) count = (int)l; else if (countObj != null && int.TryParse(countObj.ToString(), out var c)) count = c;
                    WriteLog($"ContentIndexFts 当前行数: {count}");
                }
                
                // 执行 FTS5 优化命令，减少索引碎片，提高搜索性能
                await OptimizeFtsIndexAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建 FTS5 表失败: {ex.Message}");
                WriteLog($"创建 FTS5 表失败: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 初始化默认的文件扩展名
        /// </summary>
        private async Task InitializeDefaultExtensionsAsync()
        {
            try
            {
                var defaultExtensions = new[]
                {
                    // 文本文档
                    ".txt", ".log", ".md", ".cs", ".cpp", ".h", ".hpp", ".java", ".js", ".ts", ".jsx", ".tsx",
                    ".html", ".css", ".xml", ".json", ".csv", ".ini", ".config", ".properties", ".sql", ".php", ".py",
                    ".rb", ".go", ".rs", ".sh", ".bat", ".ps1", ".asm", ".swift", ".kt", ".pl", ".r", ".lua",
                    ".perl", ".scala", ".groovy", ".vb", ".vbs", ".jsp", ".asp", ".aspx",
                    
                    // 电子邮件
                    ".eml", ".msg",
                    
                    // Microsoft Office文档
                    ".docx", ".doc", ".docm", ".dotx", ".dotm",
                    ".xlsx", ".xls", ".xlsm", ".xltx", ".xltm", ".xlsb",
                    ".pptx", ".ppt", ".pptm", ".potx", ".potm", ".ppsx", ".ppsm",
                    
                    // PDF
                    ".pdf",
                    
                    // WPS Office文档
                    ".wps", ".et", ".dps", ".wpt", ".ett", ".dpt",
                    
                    // 开放文档格式
                    ".odt", ".ods", ".odp", ".odg", ".odb", ".ofd",
                    
                    // 电子书格式
                    ".epub", ".mobi", ".chm", ".fb2", ".azw", ".azw3", ".prc", ".pdb",
                    
                    // 思维导图格式
                    ".xmind", ".mm", ".mmap", ".lighten", ".km", ".vsdx", ".drawio",
                    
                    // 图片格式
                    ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp", ".svg",
                    
                    // 二进制文件
                    ".exe", ".dll", ".so", ".sys", ".bin", ".dat", ".com", ".drv", ".ocx",
                    
                    // 压缩归档文件
                    ".zip", ".7z", ".rar", ".iso", ".tar", ".gz", ".bz2", ".xz", ".cab", ".tgz"
                };

                // 检查表中是否已有数据，避免重复插入
                var existingCount = await FileExtensions.CountAsync();
                if (existingCount > 0)
                {
                    WriteLog($"FileExtensions 表已有 {existingCount} 条数据，跳过初始化");
                    return;
                }

                // 批量插入，使用去重
                var distinctExtensions = defaultExtensions.Distinct().ToList();
                foreach (var ext in distinctExtensions)
                {
                    FileExtensions.Add(new FileExtensionEntity { Extension = ext });
                }

                await SaveChangesAsync();
                WriteLog($"已初始化 {distinctExtensions.Count} 个文件扩展名");
            }
            catch (Exception ex)
            {
                WriteLog($"初始化文件扩展名失败: {ex.Message}");
                // 不抛出异常，允许继续运行
            }
        }
        private void WriteLog(string message)
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var logPath = Path.Combine(baseDir, "error_log.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
                // 忽略日志写入错误
            }
        }
        
        /// <summary>
        /// 优化 FTS5 索引，减少碎片，提高搜索性能
        /// </summary>
        public async Task OptimizeFtsIndexAsync()
        {
            try
            {
                WriteLog("开始优化 FTS5 索引...");
                await Database.ExecuteSqlRawAsync("INSERT INTO ContentIndexFts(ContentIndexFts) VALUES('optimize')");
                WriteLog("FTS5 索引优化完成");
            }
            catch (Exception ex)
            {
                WriteLog($"FTS5 索引优化失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 文件实体
    /// </summary>
    public class FileEntity
    {
        public int Id { get; set; }
        
        [Required]
        public string Path { get; set; } = string.Empty;
        
        [Required]
        public string Name { get; set; } = string.Empty;
        
        public long Size { get; set; }
        public DateTime ModifiedDate { get; set; }
        
        [Required]
        public string Hash { get; set; } = string.Empty;
    }

    /// <summary>
    /// 内容索引实体
    /// </summary>
    public class ContentIndexEntity
    {
        public int Id { get; set; }
        public int FileId { get; set; }
        
        [Required]
        public string Content { get; set; } = string.Empty;
        public DateTime IndexedDate { get; set; }
    }

    /// <summary>
    /// 文件扩展名实体
    /// </summary>
    public class FileExtensionEntity
    {
        public int Id { get; set; }
        
        [Required]
        public string Extension { get; set; } = string.Empty;
    }
}