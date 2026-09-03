using iText.Kernel.Pdf;
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
    /// Thuật toán gom nhóm các ký tự/mẩu chữ thành từng dòng (Block) hoàn chỉnh
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

        // Sắp xếp: Trên xuống dưới (Y giảm dần trong hệ toạ độ PDF), Trái sang phải (X tăng dần)
        var sortedChunks = validChunks
            .OrderByDescending(c => c.Y)
            .ThenBy(c => c.X)
            .ToList();

        List<RawTextChunk> currentLine = new() { sortedChunks[0] };

        for (int i = 1; i < sortedChunks.Count; i++)
        {
            var prevChunk = currentLine.Last();
            var currChunk = sortedChunks[i];

            // Ngưỡng chênh lệch độ cao Y để coi là cùng 1 dòng (tolerance)
            float yTolerance = Math.Max(2.5f, currChunk.Height * 0.35f);
            bool isSameLine = Math.Abs(currChunk.Y - currentLine[0].Y) <= yTolerance;

            if (isSameLine)
            {
                currentLine.Add(currChunk);
            }
            else
            {
                // Đóng gói dòng hiện tại thành một ExtractedBlockDto
                blocks.Add(BuildBlockFromLine(currentLine, pageIndex, globalOrderIndex++));
                currentLine = new() { currChunk };
            }
        }

        // Đóng gói dòng cuối cùng
        if (currentLine.Count > 0)
        {
            blocks.Add(BuildBlockFromLine(currentLine, pageIndex, globalOrderIndex++));
        }

        return blocks;
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
                // Nếu khoảng cách giữa 2 mẩu chữ lớn hơn 15% kích thước font, thêm dấu cách
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

                var descent = renderInfo.GetDescentLine();
                var ascent = renderInfo.GetAscentLine();

                float startX = descent.GetStartPoint().Get(0);
                float startY = descent.GetStartPoint().Get(1);
                float width = descent.GetLength();
                float height = Math.Abs(ascent.GetStartPoint().Get(1) - descent.GetStartPoint().Get(1));

                float fontSize = renderInfo.GetFontSize();
                if (height <= 0.01f)
                {
                    height = fontSize > 0 ? fontSize : 10f;
                }

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
                    Y = startY,
                    Width = width,
                    Height = height,
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
        public float Width { get; set; }
        public float Height { get; set; }
        public string FontName { get; set; } = string.Empty;
        public float FontSize { get; set; }
    }
}
