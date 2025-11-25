using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Presentation;
using UglyToad.PdfPig;
using SpireDoc = Spire.Doc; // 重命名Spire.Doc命名空间
using SpirePresentation = Spire.Presentation; // 重命名Spire.Presentation命名空间
using ClosedXML.Excel;
using Tesseract;
using System.Text.RegularExpressions;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;

namespace FileSearchTool.Services
{
    /// <summary>
    /// 内容提取服务，支持多种文档格式的内容解析
    /// </summary>
    public class ContentExtractorService
    {
        /// <summary>
        /// 提取文件文本内容（支持取消）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>提取的文本内容</returns>
        public async Task<string> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件不存在: {filePath}");

            cancellationToken.ThrowIfCancellationRequested();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            try
            {
                return extension switch
                {
                    ".txt" => await ExtractTxtContentAsync(filePath, cancellationToken),
                    ".doc" => await ExtractDocContentAsync(filePath, cancellationToken),
                    ".docx" => await ExtractDocxContentAsync(filePath, cancellationToken),
                    ".xls" => await ExtractXlsContentAsync(filePath, cancellationToken),
                    ".xlsx" => await ExtractXlsxContentAsync(filePath, cancellationToken),
                    ".ppt" => await ExtractPptContentAsync(filePath, cancellationToken),
                    ".pptx" => await ExtractPptxContentAsync(filePath, cancellationToken),
                    ".pdf" => await ExtractPdfContentAsync(filePath, cancellationToken),
                    ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tiff" => await ExtractImageContentAsync(filePath, cancellationToken),
                    _ => await ExtractDefaultContentAsync(filePath, cancellationToken)
                };
            }
            catch (OperationCanceledException)
            {
                // 重新抛出取消异常
                throw;
            }
            catch (Exception ex)
            {
                // 记录错误并返回空字符串而不是抛出异常
                Console.WriteLine($"提取文件内容时出错 {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 提取TXT内容（支持取消）
        /// </summary>
        private async Task<string> ExtractTxtContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return File.ReadAllText(filePath, Encoding.UTF8);
            }, cancellationToken);
        }

        /// <summary>
        /// 提取Word内容（支持取消）
        /// </summary>
        private async Task<string> ExtractDocContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var document = new SpireDoc.Document(); // 使用重命名的命名空间
                document.LoadFromFile(filePath);
                return document.GetText();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取Word内容（支持取消）
        /// </summary>
        private async Task<string> ExtractDocxContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                using var doc = WordprocessingDocument.Open(filePath, false);
                var body = doc.MainDocumentPart?.Document.Body;
                return body?.InnerText ?? string.Empty;
            }, cancellationToken);
        }

        /// <summary>
        /// 提取Excel内容（支持取消）
        /// </summary>
        private async Task<string> ExtractXlsContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var fs = File.OpenRead(filePath);
                var workbook = new HSSFWorkbook(fs);
                var sb = new StringBuilder();
                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sheet = workbook.GetSheetAt(i);
                    if (sheet == null) continue;
                    foreach (IRow row in sheet)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (row == null) continue;
                        foreach (ICell cell in row.Cells)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            sb.Append(cell?.ToString() ?? string.Empty);
                            sb.Append(" ");
                        }
                        sb.AppendLine();
                    }
                }
                return sb.ToString();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取Excel内容（支持取消）
        /// </summary>
        private async Task<string> ExtractXlsxContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var sb = new StringBuilder();
                using var workbook = new XLWorkbook(filePath);
                
                foreach (var worksheet in workbook.Worksheets)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    foreach (var row in worksheet.RowsUsed())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        foreach (var cell in row.CellsUsed())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            sb.Append(cell.Value.ToString() ?? string.Empty);
                            sb.Append(" ");
                        }
                        sb.AppendLine();
                    }
                }
                
                return sb.ToString();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取PowerPoint内容（支持取消）
        /// </summary>
        private async Task<string> ExtractPptContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var presentation = new SpirePresentation.Presentation();
                presentation.LoadFromFile(filePath);
                var sb = new StringBuilder();
                foreach (SpirePresentation.ISlide slide in presentation.Slides)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (SpirePresentation.IShape shape in slide.Shapes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (shape is SpirePresentation.IAutoShape autoShape && autoShape.TextFrame != null)
                            sb.AppendLine(autoShape.TextFrame.Text);
                    }
                }
                return sb.ToString();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取PowerPoint内容（支持取消）
        /// </summary>
        private async Task<string> ExtractPptxContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                using var doc = PresentationDocument.Open(filePath, false);
                var presentationPart = doc.PresentationPart;
                var presentation = presentationPart?.Presentation;
                
                var sb = new StringBuilder();
                if (presentation?.SlideIdList != null)
                {
                    foreach (DocumentFormat.OpenXml.Presentation.SlideId slideId in presentation.SlideIdList.ChildElements.OfType<DocumentFormat.OpenXml.Presentation.SlideId>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var slidePart = presentationPart?.GetPartById(slideId.RelationshipId!) as SlidePart;
                        var slide = slidePart?.Slide;
                        
                        if (slide != null)
                        {
                            var text = slide.InnerText;
                            sb.AppendLine(text);
                        }
                    }
                }
                
                return sb.ToString();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取PDF内容（支持取消）
        /// </summary>
        private async Task<string> ExtractPdfContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                using var document = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                
                foreach (var page in document.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sb.AppendLine(page.Text);
                }
                
                return sb.ToString();
            }, cancellationToken);
        }

        /// <summary>
        /// 提取图片内容（OCR，支持取消）
        /// </summary>
        private async Task<string> ExtractImageContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    using var engine = new TesseractEngine(@"./tessdata", "chi_sim+eng", EngineMode.Default);
                    using var img = Pix.LoadFromFile(filePath);
                    using var page = engine.Process(img);
                    return page.GetText();
                }
                catch
                {
                    // OCR失败时返回空字符串
                    return string.Empty;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 提取默认内容（二进制文件，支持取消）
        /// </summary>
        private async Task<string> ExtractDefaultContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    return BitConverter.ToString(bytes).Replace("-", " ");
                }
                catch
                {
                    return string.Empty;
                }
            }, cancellationToken);
        }
    }
}