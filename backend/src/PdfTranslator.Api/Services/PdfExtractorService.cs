using System.Text.RegularExpressions;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PdfTranslator.Api.DTOs;

namespace PdfTranslator.Api.Services;

public class PdfExtractorService : IPdfExtractorService
{
    private readonly ILogger<PdfExtractorService> _logger;

    public PdfExtractorService(ILogger<PdfExtractorService> logger)
    {
        _logger = logger;
    }

    public Task<List<ExtractedBlockDto>> ExtractBlocksAsync(string pdfFilePath)
    {
        if (!File.Exists(pdfFilePath))
        {
            throw new FileNotFoundException($"Không tìm thấy file PDF tại: {pdfFilePath}");
        }

        var result = new List<ExtractedBlockDto>();
        int globalOrderIndex = 0;

        using (var reader = new PdfReader(pdfFilePath))
        using (var pdfDoc = new PdfDocument(reader))
        {
            int totalPages = pdfDoc.GetNumberOfPages();
            _logger.LogInformation("Bắt đầu trích xuất file PDF: {FilePath} ({TotalPages} trang)", pdfFilePath, totalPages);

            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                var page = pdfDoc.GetPage(pageNum);
                var listener = new TextBlockExtractionListener();
                var processor = new PdfCanvasProcessor(listener);
                processor.ProcessPageContent(page);

                // Gom các mẩu text rời rạc thành các Text Block có nghĩa
                var pageBlocks = GroupChunksIntoBlocks(listener.RawChunks, pageNum, ref globalOrderIndex);
                result.AddRange(pageBlocks);
            }
        }

        _logger.LogInformation("Trích xuất hoàn tất! Tổng cộng trích được {Count} block(s).", result.Count);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Vẽ các khung viền chữ nhật màu đỏ bao quanh các Text Block lên bản sao của file PDF
    /// </summary>
    public Task<string> GenerateDebugPdfAsync(string inputPdfPath, List<ExtractedBlockDto> blocks)
    {
        if (!File.Exists(inputPdfPath))
        {
            throw new FileNotFoundException($"Không tìm thấy file PDF tại: {inputPdfPath}");
        }

        var dir = Path.GetDirectoryName(inputPdfPath) ?? "";
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPdfPath);
        var debugPdfPath = Path.Combine(dir, $"{fileNameWithoutExt}_debug.pdf");

        using (var reader = new PdfReader(inputPdfPath))
        using (var writer = new PdfWriter(debugPdfPath))
        using (var pdfDoc = new PdfDocument(reader, writer))
        {
            int totalPages = pdfDoc.GetNumberOfPages();

            // Nhóm các khối văn bản theo từng trang để vẽ
            var blocksByPage = blocks.GroupBy(b => b.PageIndex);

            foreach (var pageGroup in blocksByPage)
            {
                int pageNum = pageGroup.Key;
                if (pageNum < 1 || pageNum > totalPages) continue;

                var page = pdfDoc.GetPage(pageNum);
                var canvas = new PdfCanvas(page);

                foreach (var b in pageGroup)
                {
                    // Cấu hình nét vẽ: Viền đỏ mảnh 0.8px
                    canvas.SetStrokeColor(ColorConstants.RED);
                    canvas.SetLineWidth(0.8f);

                    // Vẽ hình chữ nhật theo đúng toạ độ (X, Y, Width, Height) của BoundingBox
                    canvas.Rectangle(
                        b.BoundingBox.X,
                        b.BoundingBox.Y,
                        b.BoundingBox.Width,
                        b.BoundingBox.Height
                    );
                    canvas.Stroke();
                }
            }
        }

        _logger.LogInformation("Đã tạo file PDF Debug thành công tại: {DebugPath}", debugPdfPath);
        return Task.FromResult(debugPdfPath);
    }

    /// <summary>
    /// Thuật toán gom nhóm: Pass 1 gom thành dòng, Pass 2 gom dòng thành đoạn văn
    /// </summary>
    private List<ExtractedBlockDto> GroupChunksIntoBlocks(
        List<RawTextChunk> chunks,
        int pageIndex,
        ref int globalOrderIndex)
    {
        var blocks = new List<ExtractedBlockDto>();
        if (chunks.Count == 0) return blocks;

        // Lọc bỏ các chunk rỗng
        var validChunks = chunks
            .Where(c => !string.IsNullOrEmpty(c.Text))
            .ToList();

        if (validChunks.Count == 0) return blocks;

        // Sắp xếp các chunk sơ bộ từ trên xuống dưới (theo BaselineY giảm dần)
        var sortedChunks = validChunks
            .OrderByDescending(c => c.BaselineY)
            .ThenBy(c => c.X)
            .ToList();

        // -------------------------------------------------------------
        // PASS 1: Gom các chunk nằm trên cùng 1 hàng ngang thành các DÒNG (Lines)
        // Áp dụng thuật toán Vertical Overlap để không bị chém đôi khi khác cỡ font (Vấn đề 2)
        // -------------------------------------------------------------
        var lines = new List<List<RawTextChunk>>();
        foreach (var chunk in sortedChunks)
        {
            var matchingLine = lines.FirstOrDefault(line =>
                line.Any(existing => IsOnSameLine(existing, chunk)));

            if (matchingLine != null)
            {
                matchingLine.Add(chunk);
            }
            else
            {
                lines.Add(new List<RawTextChunk> { chunk });
            }
        }

        // Sắp xếp lại danh sách các dòng theo thứ tự từ trên xuống dưới
        lines = lines
            .OrderByDescending(l => l.Average(c => c.BaselineY))
            .ToList();

        // Chuyển từng dòng thành ExtractedBlockDto
        var lineBlocks = new List<ExtractedBlockDto>();
        for (int i = 0; i < lines.Count; i++)
        {
            lineBlocks.Add(BuildBlockFromLine(lines[i], pageIndex, i));
        }

        // -------------------------------------------------------------
        // PASS 2: Gom các dòng liên tiếp thành ĐOẠN VĂN (Paragraph Aggregation)
        // Khắc phục triệt để vấn đề chữ rớt dòng mồ côi (Vấn đề 3)
        // -------------------------------------------------------------
        var paragraphBlocks = GroupLinesIntoParagraphs(lineBlocks, ref globalOrderIndex);

        return paragraphBlocks;
    }

    /// <summary>
    /// Kiểm tra 2 mẩu chữ có nằm trên cùng 1 hàng ngang không bằng Baseline & Vertical Overlap
    /// </summary>
    private static bool IsOnSameLine(RawTextChunk a, RawTextChunk b)
    {
        // Kiểm tra độ lệch Baseline: nếu lệch dưới 35% cỡ font lớn hơn -> CÙNG DÒNG
        float baselineDiff = Math.Abs(a.BaselineY - b.BaselineY);
        float maxFontSize = Math.Max(a.FontSize, b.FontSize);
        if (baselineDiff <= maxFontSize * 0.35f)
        {
            return true;
        }

        // Kiểm tra độ trùng lặp chiều cao (Vertical Overlap)
        float topA = a.Y + a.Height;
        float bottomA = a.Y;
        float topB = b.Y + b.Height;
        float bottomB = b.Y;

        float overlap = Math.Min(topA, topB) - Math.Max(bottomA, bottomB);
        float minHeight = Math.Min(a.Height, b.Height);

        return overlap > (minHeight * 0.40f);
    }

    /// <summary>
    /// Tạo 1 ExtractedBlockDto hoàn chỉnh từ danh sách các chunk trên cùng một dòng
    /// </summary>
    private ExtractedBlockDto BuildBlockFromLine(List<RawTextChunk> lineChunks, int pageIndex, int orderIndex)
    {
        // Sắp xếp lại các phần tử trong dòng từ trái sang phải
        var orderedInLine = lineChunks.OrderBy(c => c.X).ToList();

        var textBuilder = new System.Text.StringBuilder();
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        string dominantFont = orderedInLine[0].FontName;
        float dominantFontSize = orderedInLine[0].FontSize;

        for (int i = 0; i < orderedInLine.Count; i++)
        {
            var chunk = orderedInLine[i];

            if (i > 0)
            {
                var prev = orderedInLine[i - 1];
                float gap = chunk.X - (prev.X + prev.Width);
                // Thêm khoảng trắng nếu khoảng cách giữa 2 mẩu chữ lớn hơn 15% kích thước font
                if (gap > (chunk.FontSize * 0.15f) && !textBuilder.ToString().EndsWith(" ") && !chunk.Text.StartsWith(" "))
                {
                    textBuilder.Append(' ');
                }
            }

            textBuilder.Append(chunk.Text);

            minX = Math.Min(minX, chunk.X);
            minY = Math.Min(minY, chunk.Y);
            maxX = Math.Max(maxX, chunk.X + chunk.Width);
            maxY = Math.Max(maxY, chunk.Y + chunk.Height);
        }

        return new ExtractedBlockDto
        {
            PageIndex = pageIndex,
            OrderIndex = orderIndex,
            Text = textBuilder.ToString().Replace("\0", string.Empty).Trim(),
            BlockType = "TEXT",
            BoundingBox = new BoundingBoxDto
            {
                X = (float)Math.Round(minX, 2),
                Y = (float)Math.Round(minY, 2),
                Width = (float)Math.Round(Math.Max(0, maxX - minX), 2),
                Height = (float)Math.Round(Math.Max(0, maxY - minY), 2),
                FontName = dominantFont.Replace("\0", string.Empty),
                FontSize = (float)Math.Round(dominantFontSize, 1)
            }
        };
    }

    /// <summary>
    /// Gom các dòng liên tiếp có quan hệ mạch lạc thành 1 Đoạn văn hoàn chỉnh
    /// </summary>
    private List<ExtractedBlockDto> GroupLinesIntoParagraphs(
        List<ExtractedBlockDto> lines,
        ref int globalOrderIndex)
    {
        if (lines.Count <= 1)
        {
            foreach (var l in lines) l.OrderIndex = globalOrderIndex++;
            return lines;
        }

        var paragraphs = new List<ExtractedBlockDto>();
        ExtractedBlockDto currentPara = lines[0];

        for (int i = 1; i < lines.Count; i++)
        {
            var nextLine = lines[i];

            if (ShouldMergeIntoParagraph(currentPara, nextLine))
            {
                // Hợp nhất dòng kế tiếp vào đoạn văn hiện tại
                currentPara = MergeBlocks(currentPara, nextLine);
            }
            else
            {
                currentPara.OrderIndex = globalOrderIndex++;
                paragraphs.Add(currentPara);
                currentPara = nextLine;
            }
        }

        currentPara.OrderIndex = globalOrderIndex++;
        paragraphs.Add(currentPara);

        return paragraphs;
    }

    /// <summary>
    /// Kiểm tra xem 2 dòng có phải là phần tiếp nối của cùng một đoạn văn hay không
    /// </summary>
    private static bool ShouldMergeIntoParagraph(ExtractedBlockDto prev, ExtractedBlockDto next)
    {
        // 1. Phải cùng một trang
        if (prev.PageIndex != next.PageIndex) return false;

        // 2. Không gom nếu là dòng code (font Monospace)
        bool isPrevCode = prev.BoundingBox.FontName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        bool isNextCode = next.BoundingBox.FontName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        if (isPrevCode || isNextCode) return false;

        var trimmedPrev = prev.Text.Trim();
        var trimmedNext = next.Text.Trim();

        // 3. Không gom nếu dòng trước kết thúc bằng dấu hai chấm ':' (giới thiệu danh sách / code)
        if (trimmedPrev.EndsWith(":")) return false;

        // 4. Không gom nếu dòng mới là một mục danh sách hoặc tiêu đề con
        if (trimmedNext.StartsWith("•") || trimmedNext.StartsWith("-") || trimmedNext.StartsWith("*"))
            return false;
        if (Regex.IsMatch(trimmedNext, @"^\d+[\.\)]\s"))
            return false;
        if (trimmedNext.StartsWith("Hậu quả:", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Ví dụ", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Giải pháp", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Ý tưởng", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Vấn đề", StringComparison.OrdinalIgnoreCase))
            return false;

        // 5. Nếu dòng trước kết thúc bằng dấu chấm/chấm hỏi/chấm than và có thụt lề khác nhau
        float xDiff = Math.Abs(prev.BoundingBox.X - next.BoundingBox.X);
        if (trimmedPrev.EndsWith(".") || trimmedPrev.EndsWith("!") || trimmedPrev.EndsWith("?"))
        {
            // Nếu là 2 mục thụt lề (như các bullet list) hoặc lề trái lệch nhau > 12px
            if (xDiff > 12f || prev.BoundingBox.X > 85f)
            {
                return false;
            }
        }

        // 6. Không gom nếu kích cỡ font chênh lệch đáng kể (ví dụ Tiêu đề lớn và nội dung bài)
        float fontDiff = Math.Abs(prev.BoundingBox.FontSize - next.BoundingBox.FontSize);
        if (fontDiff > 2.0f) return false;

        // 7. Tiêu đề to (fontSize >= 18) luôn đứng độc lập
        if (prev.BoundingBox.FontSize >= 18f || next.BoundingBox.FontSize >= 18f) return false;

        // 8. Kiểm tra khoảng cách dòng theo trục dọc (Vertical Line Spacing)
        float prevBottom = prev.BoundingBox.Y;
        float nextTop = next.BoundingBox.Y + next.BoundingBox.Height;
        float verticalGap = prevBottom - nextTop;

        // Khoảng cách giữa 2 dòng trong 1 đoạn văn không được quá 1.25 lần cỡ font
        float maxAllowedGap = prev.BoundingBox.FontSize * 1.25f;
        if (verticalGap > maxAllowedGap || verticalGap < -10f)
            return false;

        // 9. Lệch lề trái quá nhiều (> 25px)
        if (xDiff > 25f)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Hợp nhất 2 block thành 1 block lớn hơn có Bounding Box bao trọn cả hai
    /// </summary>
    private static ExtractedBlockDto MergeBlocks(ExtractedBlockDto a, ExtractedBlockDto b)
    {
        float minX = Math.Min(a.BoundingBox.X, b.BoundingBox.X);
        float minY = Math.Min(a.BoundingBox.Y, b.BoundingBox.Y);
        float maxX = Math.Max(a.BoundingBox.X + a.BoundingBox.Width, b.BoundingBox.X + b.BoundingBox.Width);
        float maxY = Math.Max(a.BoundingBox.Y + a.BoundingBox.Height, b.BoundingBox.Y + b.BoundingBox.Height);

        string combinedText = $"{a.Text} {b.Text}".Trim();

        return new ExtractedBlockDto
        {
            PageIndex = a.PageIndex,
            OrderIndex = a.OrderIndex,
            Text = combinedText,
            BlockType = "TEXT",
            BoundingBox = new BoundingBoxDto
            {
                X = (float)Math.Round(minX, 2),
                Y = (float)Math.Round(minY, 2),
                Width = (float)Math.Round(maxX - minX, 2),
                Height = (float)Math.Round(maxY - minY),
                FontName = a.BoundingBox.FontName,
                FontSize = a.BoundingBox.FontSize
            }
        };
    }

    /// <summary>
    /// Listener bắt các sự kiện vẽ chữ từ engine render của iText7
    /// </summary>
    private class TextBlockExtractionListener : IEventListener
    {
        public List<RawTextChunk> RawChunks { get; } = new();

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type == EventType.RENDER_TEXT && data is TextRenderInfo renderInfo)
            {
                string text = renderInfo.GetText()?.Replace("\0", string.Empty) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) return;

                var baseline = renderInfo.GetBaseline();
                float startX = baseline.GetStartPoint().Get(0);
                float baselineY = baseline.GetStartPoint().Get(1);
                float width = baseline.GetLength();

                float fontSize = renderInfo.GetFontSize();
                
                // Tính chiều cao thị giác thực tế từ AscentLine và DescentLine (User Space Coordinates)
                var ascent = renderInfo.GetAscentLine();
                var descent = renderInfo.GetDescentLine();
                float visualHeight = Math.Abs(ascent.GetStartPoint().Get(1) - descent.GetStartPoint().Get(1));


                // Nếu font size bị scale bởi Transformation Matrix (Tm) hoặc unscaled (fontSize = 1)
                if (fontSize <= 2.5f || (visualHeight > fontSize * 1.3f && visualHeight < 100f))
                {
                    fontSize = visualHeight > 0.5f ? (float)Math.Round(visualHeight, 1) : 12f;
                }

                if (fontSize <= 0.01f)
                {
                    fontSize = 12f;
                }

                // VẤN ĐỀ 1: Căn chỉnh trục Y chuẩn theo Baseline
                // Đáy chữ nằm dưới baseline khoảng 20% font size, chiều cao bao quát 1.15 lần font size
                float actualBottomY = baselineY - (fontSize * 0.20f);
                float actualHeight = Math.Max(visualHeight, fontSize * 1.15f);


                var font = renderInfo.GetFont();
                string fontName = "Unknown";
                try
                {
                    fontName = font?.GetFontProgram()?.GetFontNames()?.GetFontName() ?? "DefaultFont";
                }
                catch
                {
                    fontName = "DefaultFont";
                }

                RawChunks.Add(new RawTextChunk
                {
                    Text = text,
                    X = startX,
                    Y = actualBottomY,
                    BaselineY = baselineY,
                    Width = width,
                    Height = actualHeight,
                    FontName = fontName,
                    FontSize = fontSize
                });
            }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new HashSet<EventType> { EventType.RENDER_TEXT };
        }
    }

    private class RawTextChunk
    {
        public string Text { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public float BaselineY { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string FontName { get; set; } = string.Empty;
        public float FontSize { get; set; }
    }
}
