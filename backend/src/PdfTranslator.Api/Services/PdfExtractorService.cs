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

                // 1. Gom các mẩu text rời rạc thành các Text Block có nghĩa
                var pageTextBlocks = GroupChunksIntoBlocks(listener.RawChunks, pageNum, ref globalOrderIndex);

                // 2. Phân loại Heuristic: FORMULA_TEXT vs TEXT
                foreach (var b in pageTextBlocks)
                {
                    if (IsFormulaBlock(b))
                    {
                        b.BlockType = "FORMULA_TEXT";
                    }
                }

                // 3. Gom các Image XObject (toán tử Do) thành Image Block
                var pageImageBlocks = new List<ExtractedBlockDto>();
                foreach (var img in listener.RawImages)
                {
                    pageImageBlocks.Add(new ExtractedBlockDto
                    {
                        PageIndex = pageNum,
                        OrderIndex = 0,
                        Text = "[IMAGE]",
                        BlockType = "IMAGE",
                        BoundingBox = new BoundingBoxDto
                        {
                            X = (float)Math.Round(img.X, 2),
                            Y = (float)Math.Round(img.Y, 2),
                            Width = (float)Math.Round(img.Width, 2),
                            Height = (float)Math.Round(img.Height, 2),
                            FontName = "None",
                            FontSize = 0
                        }
                    });
                }

                // 4. Kết hợp cả Text, Formula và Image, sắp xếp theo thứ tự hiển thị từ trên xuống dưới
                var allPageBlocks = pageTextBlocks.Concat(pageImageBlocks)
                    .OrderByDescending(b => b.BoundingBox.Y + b.BoundingBox.Height)
                    .ThenBy(b => b.BoundingBox.X)
                    .ToList();

                for (int i = 0; i < allPageBlocks.Count; i++)
                {
                    allPageBlocks[i].OrderIndex = globalOrderIndex++;
                }

                result.AddRange(allPageBlocks);
            }
        }

        _logger.LogInformation("Trích xuất hoàn tất! Tổng cộng trích được {Count} block(s) (gồm {TextCount} Text, {FormulaCount} Formula, {ImageCount} Image).",
            result.Count,
            result.Count(b => b.BlockType == "TEXT"),
            result.Count(b => b.BlockType == "FORMULA_TEXT"),
            result.Count(b => b.BlockType == "IMAGE"));
        return Task.FromResult(result);
    }

    /// <summary>
    /// Vẽ các khung viền chữ nhật màu phân biệt bao quanh các Block lên bản sao của file PDF:
    /// - Đỏ: TEXT (Văn bản thông thường)
    /// - Tím: FORMULA_TEXT (Công thức toán học)
    /// - Xanh lá: IMAGE (Hình ảnh đồ họa / XObject)
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

            // Nhóm các khối theo từng trang để vẽ
            var blocksByPage = blocks.GroupBy(b => b.PageIndex);

            foreach (var pageGroup in blocksByPage)
            {
                int pageNum = pageGroup.Key;
                if (pageNum < 1 || pageNum > totalPages) continue;

                var page = pdfDoc.GetPage(pageNum);
                var canvas = new PdfCanvas(page);

                foreach (var b in pageGroup)
                {
                    // Cấu hình màu sắc viền dựa theo BlockType
                    if (b.BlockType == "IMAGE")
                    {
                        canvas.SetStrokeColor(ColorConstants.GREEN);
                        canvas.SetLineWidth(1.5f);
                    }
                    else if (b.BlockType == "FORMULA_TEXT")
                    {
                        canvas.SetStrokeColor(ColorConstants.MAGENTA);
                        canvas.SetLineWidth(1.3f);
                    }
                    else
                    {
                        canvas.SetStrokeColor(ColorConstants.RED);
                        canvas.SetLineWidth(0.8f);
                    }

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
        // 0. Không gom nếu một trong hai dòng là Công thức Toán học (FORMULA_TEXT)
        if (IsFormulaBlock(prev) || IsFormulaBlock(next)) return false;

        // 1. Phải cùng một trang
        if (prev.PageIndex != next.PageIndex) return false;

        // 2. Không gom nếu là dòng code (font Monospace)
        bool isPrevCode = prev.BoundingBox.FontName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        bool isNextCode = next.BoundingBox.FontName.Contains("Mono", StringComparison.OrdinalIgnoreCase);
        if (isPrevCode || isNextCode) return false;

        var trimmedPrev = prev.Text.Trim();
        var trimmedNext = next.Text.Trim();

        // 3. Header & Footer: Đỉnh trang (Y > 740) hoặc đáy trang (Y < 60) luôn đứng độc lập
        if (prev.BoundingBox.Y > 740f || next.BoundingBox.Y > 740f) return false;
        if (prev.BoundingBox.Y < 60f || next.BoundingBox.Y < 60f) return false;

        // 4. Mục lục (TOC): Dòng kết thúc bằng số trang (ví dụ "What is the UML? 7" hoặc "... 10")
        if (Regex.IsMatch(trimmedPrev, @"\s+\d+$") || Regex.IsMatch(trimmedNext, @"\s+\d+$"))
            return false;

        // 5. Tiêu đề mục lục toàn chữ in hoa (như "AN INTRODUCTION TO THE UML", "OBJECT ORIENTATION")
        if (trimmedPrev.Length > 3 && trimmedPrev.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            return false;
        if (trimmedNext.Length > 3 && trimmedNext.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            return false;

        // 6. Không gom nếu dòng trước kết thúc bằng dấu hai chấm ':' hoặc chấm hỏi '?'
        if (trimmedPrev.EndsWith(":") || trimmedPrev.EndsWith("?")) return false;

        // 7. Không gom nếu là Tiêu đề / Chú thích hình ảnh: Chapter, Figure, Table, Contents, Summary
        if (trimmedPrev.StartsWith("Chapter", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Chapter", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.StartsWith("Chương", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Chương", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.StartsWith("Figure", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Figure", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.StartsWith("Hình", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Hình", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.Equals("Contents", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.Equals("Mục lục", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.Equals("Summary", StringComparison.OrdinalIgnoreCase) ||
            trimmedPrev.Equals("Tóm tắt", StringComparison.OrdinalIgnoreCase))
            return false;

        // 8. Không gom nếu dòng mới là một mục danh sách hoặc tiêu đề con
        if (trimmedNext.StartsWith("•") || trimmedNext.StartsWith("-") || trimmedNext.StartsWith("*"))
            return false;
        if (trimmedPrev.StartsWith("•") && (trimmedPrev.EndsWith(".") || trimmedPrev.EndsWith(";")))
            return false;
        if (Regex.IsMatch(trimmedNext, @"^\d+[\.\)]\s"))
            return false;
        if (trimmedNext.StartsWith("Hậu quả:", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Ví dụ", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Giải pháp", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Ý tưởng", StringComparison.OrdinalIgnoreCase) ||
            trimmedNext.StartsWith("Vấn đề", StringComparison.OrdinalIgnoreCase))
            return false;

        // 9. Dấu chấm kết thúc câu: Nếu là dòng ngắn (< 380px) hoặc có khoảng cách dòng phụ -> đoạn mới
        float xDiff = Math.Abs(prev.BoundingBox.X - next.BoundingBox.X);
        float prevBottom = prev.BoundingBox.Y;
        float nextTop = next.BoundingBox.Y + next.BoundingBox.Height;
        float verticalGap = prevBottom - nextTop;

        if (trimmedPrev.EndsWith(".") || trimmedPrev.EndsWith("!"))
        {
            // Dòng ngắn kết thúc bằng dấu chấm chắc chắn là dòng cuối của đoạn văn
            if (prev.BoundingBox.Width < 380f)
            {
                return false;
            }
            // Khoảng cách đoạn văn lớn hơn 5pt
            if (verticalGap > 5.0f || xDiff > 8f)
            {
                return false;
            }
        }

        // 10. Không gom nếu kích cỡ font chênh lệch (> 1.5pt)
        float fontDiff = Math.Abs(prev.BoundingBox.FontSize - next.BoundingBox.FontSize);
        if (fontDiff > 1.5f) return false;

        // 11. Tiêu đề lớn (fontSize >= 14) luôn đứng độc lập
        if (prev.BoundingBox.FontSize >= 14f || next.BoundingBox.FontSize >= 14f) return false;

        // 12. Kiểm tra khoảng cách dòng theo trục dọc (Vertical Line Spacing)
        // Khoảng cách giữa 2 dòng trong cùng 1 đoạn văn chỉ khoảng 1-5pt
        float maxAllowedGap = Math.Min(6.5f, prev.BoundingBox.FontSize * 0.65f);
        if (verticalGap > maxAllowedGap || verticalGap < -5f)
            return false;

        // 13. Lệch lề trái quá nhiều (> 15px)
        if (xDiff > 15f)
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
    /// Heuristic phân biệt khối văn bản là Công thức Toán học (FORMULA_TEXT)
    /// Dựa vào: Hệ font toán học, dải ký tự Unicode Math/Hy Lạp, mật độ toán tử và cấu trúc token
    /// </summary>
    public static bool IsFormulaBlock(ExtractedBlockDto block)
    {
        if (string.IsNullOrWhiteSpace(block.Text)) return false;

        // 1. Kiểm tra Font toán học chuyên dụng
        if (IsMathFont(block.BoundingBox?.FontName))
        {
            return true;
        }

        var text = block.Text.Trim();

        // 2. Phân tích ký tự: Đếm số ký tự Unicode Math, Hy Lạp và toán tử
        int mathCharCount = 0;
        int greekCharCount = 0;
        int operatorCount = 0;
        int nonWhitespaceCount = 0;

        foreach (char ch in text)
        {
            if (char.IsWhiteSpace(ch)) continue;
            nonWhitespaceCount++;

            // Ký tự Hy Lạp: U+0370 -> U+03FF
            if (ch >= '\u0370' && ch <= '\u03FF')
            {
                greekCharCount++;
                mathCharCount++;
            }
            // Mathematical Operators: U+2200 -> U+22FF
            else if (ch >= '\u2200' && ch <= '\u22FF')
            {
                operatorCount++;
                mathCharCount++;
            }
            // Letterlike Symbols: U+2100 -> U+214F (ví dụ ℝ, ℂ, ℕ, ℒ)
            else if (ch >= '\u2100' && ch <= '\u214F')
            {
                mathCharCount++;
            }
            // Mathematical Arrows: U+2190 -> U+21FF
            else if (ch >= '\u2190' && ch <= '\u21FF')
            {
                operatorCount++;
                mathCharCount++;
            }
            // Superscripts and Subscripts: U+2070 -> U+209F
            else if (ch >= '\u2070' && ch <= '\u209F')
            {
                mathCharCount++;
            }
            // Các toán tử toán học chuẩn trong ASCII
            else if ("=+−*^/±×÷·~≈≠≡≤≥<>|∑∏∫∂∇√∞".Contains(ch))
            {
                operatorCount++;
                mathCharCount++;
            }
        }

        if (nonWhitespaceCount == 0) return false;

        float mathDensity = (float)mathCharCount / nonWhitespaceCount;

        // A. Nếu mật độ ký tự toán học >= 22% -> FORMULA_TEXT
        if (mathDensity >= 0.22f)
        {
            return true;
        }

        // B. Mẫu đánh số công thức ở cuối dòng (ví dụ "(1)", "(2.1)") kèm theo ít nhất 1 toán tử toán học
        bool endsWithEquationNumber = Regex.IsMatch(text, @"\(\s*\d+(\.\d+)*\s*\)$");
        if (endsWithEquationNumber && (operatorCount >= 1 || mathCharCount >= 1))
        {
            return true;
        }

        // C. Chứa ít nhất 1 ký tự toán chuyên dụng (Hy Lạp / Tích phân / Tổng / Đạo hàm / Căn) + có quan hệ phép toán
        bool hasSpecialMathSymbol = greekCharCount > 0 
            || text.Contains("∑") || text.Contains("∫") || text.Contains("∏") 
            || text.Contains("∂") || text.Contains("∇") || text.Contains("√") 
            || text.Contains("∈") || text.Contains("ℝ") || text.Contains("λ")
            || text.Contains("θ") || text.Contains("α") || text.Contains("β");

        if (hasSpecialMathSymbol)
        {
            if (text.Contains('=') || text.Contains('<') || text.Contains('>') || text.Contains('≈') || text.Contains("≤") || text.Contains("≥"))
            {
                return true;
            }

            if (mathDensity >= 0.10f)
            {
                return true;
            }
        }

        // D. Dạng công thức cấu thành từ nhiều biến đơn lẻ (short tokens)
        // Ví dụ: "f(x) = w * x + b" hoặc "y = a x^2 + b x + c"
        var tokens = text.Split(new[] { ' ', '\t', '(', ')', '[', ']', '{', '}' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length >= 3 && (text.Contains('=') || text.Contains('+') || text.Contains('-') || text.Contains('^')))
        {
            int shortTokens = tokens.Count(t => t.Length <= 2);
            float shortTokenRatio = (float)shortTokens / tokens.Length;
            if (shortTokenRatio >= 0.60f && operatorCount >= 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kiểm tra xem tên font có thuộc các bộ Font toán học chuyên dụng (LaTeX, AMS, Math fonts) không
    /// </summary>
    public static bool IsMathFont(string? fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName)) return false;
        var lower = fontName.ToLowerInvariant();

        return lower.Contains("cmmi")      // Computer Modern Math Italic
            || lower.Contains("cmsy")      // Computer Modern Math Symbols
            || lower.Contains("cmex")      // Computer Modern Math Extension
            || lower.Contains("msam")      // AMS Math Symbols A
            || lower.Contains("msbm")      // AMS Math Symbols B
            || lower.Contains("wasy")      // Wasysym
            || lower.Contains("eufm")      // Euler Fraktur
            || lower.Contains("eurm")      // Euler Roman
            || lower.Contains("eusm")      // Euler Script
            || lower.Contains("stmary")    // St Mary Road
            || lower.Contains("math")      // Generic Math fonts (CambriaMath, LatinModernMath, STIXMath, etc.)
            || lower.Contains("symbol")    // Symbol fonts
            || lower.Contains("fourier")   // Fourier Math
            || lower.Contains("txmi")
            || lower.Contains("pxmi")
            || lower.Contains("txsy")
            || lower.Contains("pxsy");
    }

    /// <summary>
    /// Listener bắt các sự kiện vẽ chữ và hình ảnh XObject từ engine render của iText7
    /// </summary>
    private class TextBlockExtractionListener : IEventListener
    {
        public List<RawTextChunk> RawChunks { get; } = new();
        public List<RawImageChunk> RawImages { get; } = new();

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

                // Căn chỉnh trục Y chuẩn theo Baseline
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
            else if (type == EventType.RENDER_IMAGE && data is ImageRenderInfo imageRenderInfo)
            {
                // Bắt sự kiện toán tử Do vẽ Image XObject
                var ctm = imageRenderInfo.GetImageCtm();
                if (ctm != null)
                {
                    float a = ctm.Get(iText.Kernel.Geom.Matrix.I11);
                    float b = ctm.Get(iText.Kernel.Geom.Matrix.I12);
                    float c = ctm.Get(iText.Kernel.Geom.Matrix.I21);
                    float d = ctm.Get(iText.Kernel.Geom.Matrix.I22);
                    float e = ctm.Get(iText.Kernel.Geom.Matrix.I31);
                    float f = ctm.Get(iText.Kernel.Geom.Matrix.I32);

                    float minX = Math.Min(Math.Min(e, e + a), Math.Min(e + c, e + a + c));
                    float maxX = Math.Max(Math.Max(e, e + a), Math.Max(e + c, e + a + c));
                    float minY = Math.Min(Math.Min(f, f + b), Math.Min(f + d, f + b + d));
                    float maxY = Math.Max(Math.Max(f, f + b), Math.Max(f + d, f + b + d));

                    float width = maxX - minX;
                    float height = maxY - minY;

                    // Lọc bỏ các phần tử quá nhỏ (stencil / mask < 6px)
                    if (width >= 6f && height >= 6f)
                    {
                        RawImages.Add(new RawImageChunk
                        {
                            X = minX,
                            Y = minY,
                            Width = width,
                            Height = height
                        });
                    }
                }
            }
        }

        public ICollection<EventType> GetSupportedEvents()
        {
            return new HashSet<EventType> { EventType.RENDER_TEXT, EventType.RENDER_IMAGE };
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

    private class RawImageChunk
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }
}

