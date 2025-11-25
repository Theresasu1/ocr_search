using System;
using System.IO;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 数据库配置服务 - 管理数据库存储路径
    /// </summary>
    public class DatabaseConfigService
    {
        private const string ConfigFileName = "database.config";
        private static readonly string DefaultDatabasePath = Path.Combine(GetProjectRootDirectory(), "db_data");
        
        /// <summary>
        /// 获取项目根目录
        /// </summary>
        private static string GetProjectRootDirectory()
        {
            // 获取当前应用程序域的基目录
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            
            // 在开发环境中，基目录通常是 bin\Debug\net6.0-windows\
            // 我们需要向上导航到项目根目录
            var directoryInfo = new DirectoryInfo(baseDirectory);
            
            // 向上导航两级: bin\Debug\net6.0-windows\ -> bin\ -> 项目根目录
            while (directoryInfo != null && 
                   (directoryInfo.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) || 
                    directoryInfo.Name.Contains("Debug") || 
                    directoryInfo.Name.Contains("Release")))
            {
                directoryInfo = directoryInfo.Parent;
            }
            
            // 如果没找到项目根目录，则使用基目录
            return directoryInfo?.FullName ?? baseDirectory;
        }
        private static string? _cachedDatabasePath;
        
        /// <summary>
        /// 获取数据库存储根目录
        /// </summary>
        public static string GetDatabaseDirectory()
        {
            if (_cachedDatabasePath != null)
            {
                return _cachedDatabasePath;
            }
            
            try
            {
                // 尝试从配置文件读取
                if (File.Exists(ConfigFileName))
                {
                    var lines = File.ReadAllLines(ConfigFileName);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("DatabasePath=", StringComparison.OrdinalIgnoreCase))
                        {
                            var path = line.Substring("DatabasePath=".Length).Trim();
                            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(Path.GetDirectoryName(path) ?? path))
                            {
                                _cachedDatabasePath = path;
                                return _cachedDatabasePath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取数据库配置失败: {ex.Message}");
            }
            
            // 使用默认路径（D盘）
            _cachedDatabasePath = DefaultDatabasePath;
            
            // 如果D盘不可用，降级到C盘
            if (!IsDriveAvailable("D:\\"))
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _cachedDatabasePath = Path.Combine(appDataPath, "FileSearchTool");
                Console.WriteLine($"D盘不可用，使用 AppData 路径: {_cachedDatabasePath}");
            }
            
            return _cachedDatabasePath;
        }
        
        /// <summary>
        /// 获取完整的数据库文件路径
        /// </summary>
        public static string GetDatabaseFilePath()
        {
            var directory = GetDatabaseDirectory();
            
            // 确保目录存在
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建数据库目录失败: {ex.Message}");
            }
            
            return Path.Combine(directory, "search_index.db");
        }
        
        /// <summary>
        /// 设置数据库存储路径
        /// </summary>
        public static bool SetDatabasePath(string path)
        {
            try
            {
                // 验证路径
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }
                
                // 确保目录存在
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                // 检查写入权限
                var testFile = Path.Combine(path, ".write_test");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch
                {
                    return false; // 没有写入权限
                }
                
                // 保存到配置文件
                File.WriteAllText(ConfigFileName, $"DatabasePath={path}");
                
                // 更新缓存
                _cachedDatabasePath = path;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置数据库路径失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 获取磁盘剩余空间（字节）
        /// </summary>
        public static long GetDiskFreeSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path) ?? "C:\\");
                return drive.AvailableFreeSpace;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 获取磁盘剩余空间（GB）
        /// </summary>
        public static double GetDiskFreeSpaceGB(string path)
        {
            return GetDiskFreeSpace(path) / (1024.0 * 1024.0 * 1024.0);
        }
        
        /// <summary>
        /// 检查驱动器是否可用
        /// </summary>
        private static bool IsDriveAvailable(string path)
        {
            try
            {
                var driveLetter = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(driveLetter))
                {
                    return false;
                }
                
                var drive = new DriveInfo(driveLetter);
                return drive.IsReady;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取数据库文件大小（MB）
        /// </summary>
        public static double GetDatabaseSizeMB()
        {
            try
            {
                var dbPath = GetDatabaseFilePath();
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    return fileInfo.Length / (1024.0 * 1024.0);
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return 0;
        }
        
        /// <summary>
        /// 保存索引路径
        /// </summary>
        /// <param name="indexPath">索引路径</param>
        public void SaveIndexPath(string indexPath)
        {
            try
            {
                // 读取现有配置
                var lines = new List<string>();
                if (File.Exists(ConfigFileName))
                {
                    lines = new List<string>(File.ReadAllLines(ConfigFileName));
                }
                
                // 查找并更新IndexPath行，或者添加新行
                bool found = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith("IndexPath=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"IndexPath={indexPath}";
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    lines.Add($"IndexPath={indexPath}");
                }
                
                // 保存配置
                File.WriteAllLines(ConfigFileName, lines);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存索引路径失败: {ex.Message}");
            }
        }
    }
}
