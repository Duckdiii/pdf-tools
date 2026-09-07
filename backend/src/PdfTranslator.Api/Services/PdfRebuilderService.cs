using System.Text.Json;
using System.Text.RegularExpressions;
using iText.IO.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using PdfTranslator.Api.DTOs;
using PdfTranslator.Api.Models;

namespace PdfTranslator.Api.Services;

public class PdfRebuilderService : IPdfRebuilderService
{
    private readonly ILogger<PdfRebuilderService> _logger;

    public PdfRebuilderService(ILogger<PdfRebuilderService> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateTranslatedPdfAsync(string originalPdfPath, List<ContentBlock> blocks)
    {
        if (!System.IO.File.Exists(originalPdfPath))
        {
            throw new FileNotFoundException($"Không tìm thấy file PDF gốc tại: {originalPdfPath}");
        }

        var dir = System.IO.Path.GetDirectoryName(originalPdfPath) ?? "";
        var fileNameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(originalPdfPath);
        
        // Tránh tạo tên file lồng nhau nếu file đã có hậu tố _translated
        var cleanBaseName = fileNameWithoutExt.Replace("_translated", "");
        var translatedPdfPath = System.IO.Path.Combine(dir, $"{cleanBaseName}_translated.pdf");

        // Tải font Unicode tiếng Việt (Regular và Bold)
        var (regularFont, boldFont) = LoadVietnameseFonts();

        using (var reader = new PdfReader(originalPdfPath))
        using (var writer = new PdfWriter(translatedPdfPath))
        using (var pdfDoc = new PdfDocument(reader, writer))
        {
            int totalPages = pdfDoc.GetNumberOfPages();
            var blocksByPage = blocks.GroupBy(b => b.PageIndex).ToDictionary(g => g.Key, g => g.OrderBy(b => b.OrderIndex).ToList());

            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                if (!blocksByPage.TryGetValue(pageNum, out var pageBlocks) || pageBlocks.Count == 0)
                {
                    continue;
                }

                var page = pdfDoc.GetPage(pageNum);
                var pageSize = page.GetPageSize();

                foreach (var block in pageBlocks)
                {
                    if (string.IsNullOrWhiteSpace(block.BoundingBoxJson)) continue;

                    BoundingBoxDto? box = null;
                    try
                    {
                        box = JsonSerializer.Deserialize<BoundingBoxDto>(block.BoundingBoxJson);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Lỗi parse BoundingBoxJson của block {BlockId}", block.Id);
                        continue;
                    }

                    if (box == null || box.Width <= 0 || box.Height <= 0) continue;

                    // Nội dung cần vẽ: Ưu tiên TranslatedText, fallback OriginalText
                    var textToRender = !string.IsNullOrWhiteSpace(block.TranslatedText)
                        ? block.TranslatedText
                        : block.OriginalText;

                    if (string.IsNullOrWhiteSpace(textToRender)) continue;

                    // 2. CHỌN FONT PHÙ HỢP (Bold hoặc Regular)
                    bool isBold = box.FontName.Contains("Bold", StringComparison.OrdinalIgnoreCase)
                        || box.FontName.Contains("700")
                        || box.FontName.Contains("600")
                        || box.FontSize >= 18f;
                    var chosenFont = isBold ? boldFont : regularFont;

                    // Cỡ chữ ước lượng
                    float fontSize = box.FontSize > 2.5f ? box.FontSize : 14f;

                    // 1. CHE VĂN BẢN CŨ BẰNG NỀN TRẮNG (Whiteout)
                    // Giữ hộp che trắng vừa vặn BoundingBox thực tế, không lấn sang các đường kẻ ngang/dọc trang trí
                    float coverX = Math.Max(0, box.X - 1.0f);
                    float coverY = box.Y;
                    float coverW = Math.Min(pageSize.GetWidth() - coverX, box.Width + 2.0f);
                    float coverH = box.Height;

                    var pdfCanvas = new PdfCanvas(page);
                    pdfCanvas.SaveState();
                    pdfCanvas.SetFillColor(ColorConstants.WHITE);
                    pdfCanvas.Rectangle(coverX, coverY, coverW, coverH);
                    pdfCanvas.Fill();
                    pdfCanvas.RestoreState();

                    // 3. TÍNH TOÁN CỠ CHỮ VÀ KHUNG VẼ (Fit & No-clipping)
                    float maxAvailableWidth = Math.Max(box.Width, pageSize.GetWidth() - box.X - 36f);

                    // Mở rộng chiều ngang cho đoạn văn bản hoặc tiêu đề nếu lề phải còn trống
                    float renderW = box.Width;
                    if (box.Width > 280f)
                    {
                        renderW = Math.Min(maxAvailableWidth, Math.Max(box.Width, 460f));
                    }
                    else
                    {
                        renderW = Math.Min(maxAvailableWidth, box.Width * 1.20f);
                    }

                    // Nhận diện dòng Mục lục (TOC): Bắt đầu bằng tên mục và kết thúc bằng số trang
                    var tocMatch = Regex.Match(textToRender, @"^(.*?)\s+(\d+)$");
                    bool isTocLine = tocMatch.Success && box.Width > 150f && (box.Height <= fontSize * 2.5f);

                    float currentFontSize = fontSize;
                    float lineLeading = 1.15f;
                    bool isSingleLine = box.Height <= fontSize * 1.6f;

                    // THUẬT TOÁN CO CHỮ THÔNG MINH (Auto Font-Fitting)
                    // Giữ nguyên ranh giới hộp gốc: Không bao giờ để khối trên nở tràn đè lên khối dưới
                    if (!isSingleLine && !isTocLine)
                    {
                        float targetHeight = box.Height + 1.5f;
                        int maxIterations = 12;
                        while (maxIterations-- > 0 && currentFontSize > 7.5f)
                        {
                            float totalWidth = chosenFont.GetWidth(textToRender, currentFontSize);
                            float estLines = (float)Math.Ceiling(totalWidth / renderW);
                            float estHeight = estLines * (currentFontSize * lineLeading);

                            if (estHeight <= targetHeight)
                            {
                                break;
                            }

                            currentFontSize -= 0.5f;
                            if (lineLeading > 1.0f)
                            {
                                lineLeading -= 0.03f;
                            }
                        }
                    }
                    else if (isSingleLine || isTocLine)
                    {
                        // Dòng đơn / Mục lục: co cỡ font nếu text dài quá renderW
                        while (currentFontSize > 7.5f && chosenFont.GetWidth(textToRender, currentFontSize) > renderW)
                        {
                            currentFontSize -= 0.5f;
                        }
                    }

                    // Khóa cứng chiều cao hiển thị trong phạm vi hộp gốc để chống đè chữ
                    float renderH = Math.Max(box.Height, currentFontSize * lineLeading);
                    float topY = box.Y + box.Height;
                    float adjustedBottomY = topY - renderH;

                    var textRect = new iText.Kernel.Geom.Rectangle(box.X, adjustedBottomY, renderW, renderH);

                    using (var layoutCanvas = new Canvas(page, textRect))
                    {
                        if (isTocLine)
                        {
                            // Định dạng mục lục: Tên mục căn trái, số trang căn phải thẳng hàng
                            string titlePart = tocMatch.Groups[1].Value.Trim();
                            string pagePart = tocMatch.Groups[2].Value.Trim();

                            float pageColW = Math.Max(30f, chosenFont.GetWidth(pagePart, currentFontSize) + 6f);
                            float titleColW = Math.Max(50f, renderW - pageColW);

                            var tocTable = new Table(new float[] { titleColW, pageColW })
                                .SetWidth(renderW)
                                .SetBorder(iText.Layout.Borders.Border.NO_BORDER);

                            var cellTitle = new Cell()
                                .Add(new Paragraph(titlePart)
                                    .SetFont(chosenFont)
                                    .SetFontSize(currentFontSize)
                                    .SetFontColor(ColorConstants.BLACK)
                                    .SetMargin(0)
                                    .SetPadding(0))
                                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                                .SetPadding(0)
                                .SetMargin(0)
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.LEFT);

                            var cellPage = new Cell()
                                .Add(new Paragraph(pagePart)
                                    .SetFont(chosenFont)
                                    .SetFontSize(currentFontSize)
                                    .SetFontColor(ColorConstants.BLACK)
                                    .SetMargin(0)
                                    .SetPadding(0))
                                .SetBorder(iText.Layout.Borders.Border.NO_BORDER)
                                .SetPadding(0)
                                .SetMargin(0)
                                .SetTextAlignment(iText.Layout.Properties.TextAlignment.RIGHT);

                            tocTable.AddCell(cellTitle);
                            tocTable.AddCell(cellPage);
                            layoutCanvas.Add(tocTable);
                        }
                        else
                        {
                            var paragraph = new Paragraph(textToRender)
                                .SetFont(chosenFont)
                                .SetFontSize(currentFontSize)
                                .SetFontColor(ColorConstants.BLACK)
                                .SetMargin(0)
                                .SetPadding(0)
                                .SetMultipliedLeading(lineLeading);

                            layoutCanvas.Add(paragraph);
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Đã tái tạo file PDF Tiếng Việt thành công tại: {Path}", translatedPdfPath);
        return Task.FromResult(translatedPdfPath);
    }

    /// <summary>
    /// Tải font Unicode Tiếng Việt (Regular và Bold)
    /// </summary>
    private (PdfFont regular, PdfFont bold) LoadVietnameseFonts()
    {
        PdfFont? regular = null;
        PdfFont? bold = null;

        // 1. Kiểm tra font Arial trên Windows
        string winArial = @"C:\Windows\Fonts\arial.ttf";
        string winArialBd = @"C:\Windows\Fonts\arialbd.ttf";

        if (System.IO.File.Exists(winArial))
        {
            try
            {
                regular = PdfFontFactory.CreateFont(winArial, PdfEncodings.IDENTITY_H);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải font Windows Arial Regular.");
            }
        }

        if (System.IO.File.Exists(winArialBd))
        {
            try
            {
                bold = PdfFontFactory.CreateFont(winArialBd, PdfEncodings.IDENTITY_H);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải font Windows Arial Bold.");
            }
        }

        // 2. Fallback nếu thiếu font
        regular ??= PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        bold ??= regular;

        return (regular, bold);
    }
}