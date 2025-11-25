# OCR文件搜索工具

OCR文件搜索工具是一款功能强大的本地文件全文检索应用，支持多种文档格式的内容提取与快速搜索。该工具集成了OCR光学字符识别技术，能够从图片和扫描文档中提取文本内容，为用户提供高效便捷的文件管理和检索体验。

## 核心功能

### 📄 多格式文档支持
- **Microsoft Office文档**：Word (.docx, .doc)、Excel (.xlsx, .xls)、PowerPoint (.pptx, .ppt)
- **PDF文档**：支持文本型和扫描型PDF
- **文本文件**：.txt、.log、.md等各类文本格式
- **开放文档格式**：.odt、.ods、.odp等
- **图片格式**：.jpg、.png、.bmp、.gif、.tiff等（OCR识别）

### 🔍 全文搜索
- 基于SQLite FTS5全文索引引擎
- 支持中英文混合搜索
- 多关键词组合查询
- 快速响应，毫秒级搜索速度

### 🖼️ OCR光学识别
- 集成Tesseract OCR引擎
- 支持中英文字符识别
- 自动识别图片和扫描文档中的文字内容
- 识别结果可搜索

### ⚙️ 智能索引管理
- 自定义索引目录范围
- 智能排除系统目录（默认排除C盘系统目录）
- 文件类型白名单配置
- 自动检测文件变更，增量更新索引
- 文件大小阈值控制（默认200MB）

### 🚀 性能优化
- 并发索引处理（可配置CPU核心数）
- 内容压缩存储，节省磁盘空间
- WAL模式数据库，提升并发性能
- 异步加载，避免界面卡顿

### 🎨 用户体验
- **系统托盘**：最小化到系统托盘，后台运行
- **全局快捷键**：默认Ctrl+Alt+D快速唤醒（可自定义）
- **定时索引**：支持设置定时任务自动更新索引
- **深色主题**：支持浅色/深色主题切换
- **最小化选项**：支持最小化到任务栏

## 安装与部署

### 系统要求
- 操作系统：Windows 10/11 (x64)
- .NET运行时：无需安装（自包含部署）

### 安装步骤
1. 下载最新版本的安装程序 `FileSearchTool_Setup_vX.X.X.exe`
2. 运行安装程序
3. 选择安装目录
4. 可选：创建桌面快捷方式、开机自启动
5. 完成安装

### 首次使用
1. 启动程序
2. 添加需要索引的目录
3. 点击工具"建立索引"开始建立索引
4. 索引完成后即可开始搜索

### 开发环境运行
1. 确保已安装 .NET 6 SDK
2. 在项目根目录执行：
   ```
   dotnet run --project FileSearchTool
   ```

### 发布版本运行
1. 使用以下命令发布应用：
   ```
   cd FileSearchTool
   dotnet publish -c Release -r win-x64 --self-contained true
   ```
2. 运行发布版本：
   ```
   cd bin\Release\net6.0-windows\win-x64\publish
   .\FileSearchTool.exe
   ```

### 生成安装程序
```powershell
# 使用Inno Setup生成安装包
.\build_installer.bat
```

## 配置说明

### settings.config
主要配置文件，包含以下设置：
- `IndexAllFiles`: 是否索引所有文件
- `AllowedExtensions`: 允许索引的文件扩展名列表
- `MaxFileSizeBytes`: 最大文件大小限制（字节）
- `ExcludedSubdirs`: 排除的子目录列表

### hotkey.config
全局快捷键配置文件

### 关键配置
- **数据库默认路径**：`D:\SearchIndex\search_index.db`
- **日志文件**：程序目录下的 `error_log.txt`
- **配置文件**：`performance.config`、`database.config`

## OCR功能说明

本工具集成了Tesseract OCR引擎，支持多种语言的文本识别：

1. 中文简体识别 (chi_sim)
2. 英文识别 (eng)

训练数据文件位于 `tessdata` 目录中。

## 使用说明

### 搜索功能
- **基本搜索**：在搜索框输入关键词，按回车或点击搜索按钮
- **多关键词搜索**：使用空格分隔多个关键词
- **中英文搜索**：自动支持中英文混合搜索
- **查看结果**：双击搜索结果直接打开文件

### 索引管理
- **添加索引目录**：首选项 → 索引管理 → 添加目录
- **删除索引**：可按目录或全部删除
- **重建索引**：清空后重新索引
- **查看统计**：显示已索引文件数、总大小等信息

### 文件类型配置
- **白名单模式**：只索引指定类型的文件
- **全部文件模式**：索引所有文件（不推荐）
- **文件大小限制**：设置单文件索引上限（默认200MB）

### 性能设置
- **并发核心数**：设置索引时使用的CPU核心数（默认50%）
- **数据库路径**：自定义索引数据库存储位置（默认D:\SearchIndex）

### 快捷键设置
- **全局快捷键**：自定义唤醒程序的快捷键组合
- **默认快捷键**：Ctrl+Alt+D

### 定时索引
- **启用定时任务**：设置每日自动索引时间
- **增量更新**：自动检测文件变更

## 技术架构

### 开发环境
- **框架**：.NET 6
- **UI框架**：WPF (Windows Presentation Foundation)
- **设计模式**：MVVM (Model-View-ViewModel)

### 核心技术栈
- **数据库**：SQLite + Entity Framework Core
- **全文搜索**：SQLite FTS5
- **OCR引擎**：Tesseract 5.2.0
- **Office文档解析**：
  - DocumentFormat.OpenXml (Office Open XML)
  - NPOI (Excel .xls)
  - FreeSpire (Word .doc)
  - Spire.Presentation (PowerPoint .ppt)
- **PDF解析**：PdfPig + Docnet.Core
- **UI组件**：iNKORE.UI.WPF.Modern, AntdUI

### 项目结构
```
FileSearchTool/
├── Services/              # 服务层
│   ├── ContentExtractorService.cs      # 内容提取服务
│   ├── SearchService.cs                # 搜索服务
│   ├── IndexingService.cs              # 索引服务
│   ├── IndexStorageService.cs          # 索引存储服务
│   ├── FileScannerService.cs           # 文件扫描服务
│   ├── GlobalHotKeyService.cs          # 全局快捷键服务
│   ├── TrayIconService.cs              # 系统托盘服务
│   ├── ScheduledIndexingService.cs     # 定时索引服务
│   └── DatabaseConfigService.cs        # 数据库配置服务
├── ViewModel/             # 视图模型层
│   ├── MainViewModel.cs                # 主视图模型
│   └── SearchResultViewModel.cs        # 搜索结果视图模型
├── Model/                 # 数据模型层
│   └── SearchResult.cs                 # 搜索结果模型
├── Data/                  # 数据访问层
│   └── SearchDbContext.cs              # 数据库上下文
├── Windows/               # 窗口视图
│   ├── PreferencesWindow.xaml          # 首选项窗口
│   ├── IndexManagementControl.xaml     # 索引管理控件
│   ├── FileTypeSettingsControl.xaml    # 文件类型设置控件
│   ├── HotKeySettingsControl.xaml      # 快捷键设置控件
│   └── PerformanceSettingsControl.xaml # 性能设置控件
└── Helpers/               # 辅助类
    ├── AnimationHelper.cs              # 动画辅助类
    └── BooleanConverters.cs            # 布尔转换器
```

### 编译项目
```powershell
# 开发运行
dotnet run --project FileSearchTool

# 发布版本
dotnet publish -c Release -r win-x64 --self-contained true
```

## 版本历史

### v5.0.0 (最新)
- 新增搜索框回车键触发搜索功能
- 优化默认数据库存储路径为项目db_data目录
- 修复安装程序版本号问题

### v4.0.0
- 修复DbContext池化导致的崩溃问题
- 优化数据库初始化逻辑
- 移除启动时的黑框显示
- 清理旧版本遗留数据

### v3.0.0
- 修复启动黑框问题
- 优化索引性能

### v2.0.0
- 新增定时索引功能
- 支持深色主题
- 优化搜索性能

### v1.0.0
- 初始版本发布
- 基础搜索和索引功能

## 注意事项

⚠️ **重要提示**
- 首次索引大量文件可能需要较长时间，请耐心等待
- 建议设置合理的文件大小阈值，避免索引超大文件
- 默认排除C盘系统目录，避免索引系统文件
- 定期优化索引以保持搜索性能
- 索引数据库会占用一定磁盘空间，建议定期清理不需要的索引

## 常见问题

**Q: 搜索结果不准确？**
A: 尝试重建索引或检查文件是否在索引范围内。

**Q: 索引速度慢？**
A: 可以增加并发核心数，或减少索引文件的大小阈值。

**Q: OCR识别准确率低？**
A: OCR识别效果受图片质量影响，建议使用清晰的图片或扫描件。

**Q: 程序占用内存过高？**
A: 索引大量文件时会占用较多内存，完成后会自动释放。可以减少并发核心数降低内存占用。

**Q: 数据库文件过大？**
A: 删除不需要的索引目录，或重建索引并启用内容压缩。

## 许可协议

本项目仅供学习和个人使用。

## 联系方式

如有问题或建议，请通过以下方式联系：
- 项目地址：https://github.com/
- 问题反馈：提交Issue

---

**© 2025 FileSearchTool Dev Team**
