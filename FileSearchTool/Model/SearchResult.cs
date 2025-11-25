using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FileSearchTool.Model
{
    public enum MatchType
    {
        FileName,
        Content,
        Index
    }

    public class SearchResult
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public float Score { get; set; }
        public MatchType MatchType { get; set; }
        public string? Content { get; set; }
        public string? Snippet { get; set; }
        
        // 添加文件图标相关属性
        public string FileExtension => Path.GetExtension(FilePath).ToLower();
        public string FileIcon => GetFileIconPath(FileExtension);
        
        private string GetFileIconPath(string extension)
        {
            return extension switch
            {
                ".doc" or ".docx" => "/Resources/word.png",
                ".xls" or ".xlsx" => "/Resources/excel.png",
                ".ppt" or ".pptx" => "/Resources/powerpoint.png",
                ".pdf" => "/Resources/pdf.png",
                _ => "/Resources/file.png"
            };
        }
    }
}