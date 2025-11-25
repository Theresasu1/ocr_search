using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Color = System.Windows.Media.Color;  // 明确使用WPF的Color

namespace FileSearchTool.Services
{
    /// <summary>
    /// 语法高亮服务
    /// </summary>
    public class SyntaxHighlightService
    {
        // 定义不同语言的关键字
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
            "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte",
            "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while"
        };

        private static readonly HashSet<string> PythonKeywords = new HashSet<string>
        {
            "and", "as", "assert", "break", "class", "continue", "def", "del", "elif", "else",
            "except", "exec", "finally", "for", "from", "global", "if", "import", "in", "is",
            "lambda", "not", "or", "pass", "print", "raise", "return", "try", "while", "with",
            "yield", "None", "True", "False"
        };

        private static readonly HashSet<string> JavaScriptKeywords = new HashSet<string>
        {
            "abstract", "arguments", "await", "boolean", "break", "byte", "case", "catch", "char",
            "class", "const", "continue", "debugger", "default", "delete", "do", "double", "else",
            "enum", "eval", "export", "extends", "false", "final", "finally", "float", "for",
            "function", "goto", "if", "implements", "import", "in", "instanceof", "int",
            "interface", "let", "long", "native", "new", "null", "package", "private", "protected",
            "public", "return", "short", "static", "super", "switch", "synchronized", "this",
            "throw", "throws", "transient", "true", "try", "typeof", "var", "void", "volatile",
            "while", "with", "yield"
        };

        private static readonly HashSet<string> JavaKeywords = new HashSet<string>
        {
            "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class",
            "const", "continue", "default", "do", "double", "else", "enum", "extends", "final",
            "finally", "float", "for", "goto", "if", "implements", "import", "instanceof", "int",
            "interface", "long", "native", "new", "null", "package", "private", "protected",
            "public", "return", "short", "static", "strictfp", "super", "switch", "synchronized",
            "this", "throw", "throws", "transient", "try", "void", "volatile", "while"
        };

        // 根据文件扩展名获取语言类型
        public static string GetLanguageByExtension(string extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".cs" => "csharp",
                ".py" => "python",
                ".js" => "javascript",
                ".ts" => "javascript",
                ".java" => "java",
                ".cpp" or ".cxx" or ".cc" => "cpp",
                ".c" => "c",
                ".h" or ".hpp" => "cpp",
                ".html" or ".htm" => "html",
                ".xml" => "xml",
                ".css" => "css",
                ".sql" => "sql",
                ".php" => "php",
                ".rb" => "ruby",
                ".go" => "go",
                ".rs" => "rust",
                _ => "text"
            };
        }

        // 应用语法高亮
        public static void ApplySyntaxHighlight(Paragraph paragraph, string content, string language)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var inlines = ParseLine(line, language);
                paragraph.Inlines.AddRange(inlines);
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        // 解析单行并应用高亮
        private static List<Inline> ParseLine(string line, string language)
        {
            var inlines = new List<Inline>();
            var keywords = GetKeywordsForLanguage(language);

            // 简化的解析逻辑，实际项目中可以使用更复杂的解析器
            var words = Regex.Split(line, @"(\s+)");
            var inString = false;
            var inComment = false;
            var stringChar = '\0';

            foreach (var word in words)
            {
                if (string.IsNullOrEmpty(word))
                    continue;

                // 检查是否在字符串中
                if (inString)
                {
                    inlines.Add(CreateRun(word, Colors.Red)); // 字符串颜色
                    if (word.EndsWith(stringChar.ToString()) && !word.EndsWith("\\" + stringChar))
                    {
                        inString = false;
                        stringChar = '\0';
                    }
                    continue;
                }

                // 检查是否在注释中
                if (inComment)
                {
                    inlines.Add(CreateRun(word, Colors.Green)); // 注释颜色
                    continue;
                }

                // 检查字符串开始
                if ((word.StartsWith("\"") || word.StartsWith("'") || word.StartsWith("`")) && 
                    !(word.Length > 1 && (word.EndsWith("\"") || word.EndsWith("'") || word.EndsWith("`")) && 
                      !word.EndsWith("\\" + word[0])))
                {
                    inString = true;
                    stringChar = word[0];
                    inlines.Add(CreateRun(word, Colors.Red)); // 字符串颜色
                    continue;
                }

                // 检查注释开始
                if (IsCommentStart(word, language))
                {
                    inComment = true;
                    inlines.Add(CreateRun(word, Colors.Green)); // 注释颜色
                    continue;
                }

                // 检查关键字
                if (keywords.Contains(word))
                {
                    inlines.Add(CreateRun(word, Colors.Blue)); // 关键字颜色
                }
                // 检查数字
                else if (Regex.IsMatch(word, @"^\d+(\.\d+)?$"))
                {
                    inlines.Add(CreateRun(word, Colors.Purple)); // 数字颜色
                }
                // 普通文本
                else
                {
                    inlines.Add(new Run(word));
                }
            }

            return inlines;
        }

        // 为指定语言获取关键字集合
        private static HashSet<string> GetKeywordsForLanguage(string language)
        {
            return language?.ToLowerInvariant() switch
            {
                "csharp" => CSharpKeywords,
                "python" => PythonKeywords,
                "javascript" => JavaScriptKeywords,
                "java" => JavaKeywords,
                _ => new HashSet<string>()
            };
        }

        // 检查是否是注释开始
        private static bool IsCommentStart(string word, string language)
        {
            return language?.ToLowerInvariant() switch
            {
                "csharp" or "java" or "javascript" or "cpp" or "c" => word.StartsWith("//") || word.StartsWith("/*"),
                "python" => word.StartsWith("#"),
                "sql" => word.StartsWith("--"),
                _ => false
            };
        }

        // 创建带颜色的Run
        private static Run CreateRun(string text, Color color)
        {
            return new Run(text)
            {
                Foreground = new SolidColorBrush(color)
            };
        }
    }
}