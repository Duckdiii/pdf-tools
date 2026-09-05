using System.Text.Json;
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
                    float coverX = Math.Max(0, box.X - 2.0f);
                    float coverY = Math.Max(0, box.Y - 2.0f);
                    float coverW = Math.Min(pageSize.GetWidth() - coverX, box.Width + 4.0f);
                    float coverH = Math.Max(box.Height + 4.0f, fontSize * 1.35f);

                    var pdfCanvas = new PdfCanvas(page);
                    pdfCanvas.SaveState();
                    pdfCanvas.SetFillColor(ColorConstants.WHITE);
                    pdfCanvas.Rectangle(coverX, coverY, coverW, coverH);
                    pdfCanvas.Fill();
                    pdfCanvas.RestoreState();

                    // 3. TÍNH TOÁN CỠ CHỮ VÀ KHUNG VẼ (Fit & No-clipping)
                    float maxAvailableWidth = pageSize.GetWidth() - box.X - 36f; // Chừa lề phải tối thiểu 36pt


                    // Đối với tiêu đề hoặc dòng đơn: Cho phép mở rộng chiều ngang nếu tiếng Việt dài hơn
                    float renderW = Math.Max(box.Width, Math.Min(maxAvailableWidth, box.Width * 1.35f));

                    // Nếu là tiêu đề 1 dòng (chiều cao hộp nhỏ), tự động co nhẹ font nếu chữ dài vượt khung
                    bool isSingleLine = box.Height <= fontSize * 1.6f;
                    if (isSingleLine)
                    {
                        float textWidth = chosenFont.GetWidth(textToRender, fontSize);
                        while (textWidth > renderW && fontSize > 10f)
                        {
                            fontSize -= 0.5f;
                            textWidth = chosenFont.GetWidth(textToRender, fontSize);
                        }
                    }
                    else if (textToRender.Length > (block.OriginalText?.Length ?? 0) * 1.25f && fontSize > 8f)
                    {
                        fontSize = Math.Max(8f, fontSize * 0.92f);
                    }

                    // Tính chiều cao cần thiết cho văn bản để không bao giờ bị cắt dòng (no-clipping)
                    float singleLineHeight = fontSize * 1.20f;
                    float approxLines = (float)Math.Ceiling(chosenFont.GetWidth(textToRender, fontSize) / renderW);
                    float neededHeight = Math.Max(box.Height + 4.0f, approxLines * singleLineHeight + 4.0f);

                    // Điểm neo đỉnh (Top Y) giữ nguyên, đáy (Bottom Y) mở rộng xuống dưới
                    float topY = box.Y + box.Height;
                    float adjustedBottomY = Math.Max(10f, topY - neededHeight);
                    float renderH = topY - adjustedBottomY;

                    var textRect = new iText.Kernel.Geom.Rectangle(box.X, adjustedBottomY, renderW, renderH);

                    using (var layoutCanvas = new Canvas(page, textRect))
                    {
                        var paragraph = new Paragraph(textToRender)
                            .SetFont(chosenFont)
                            .SetFontSize(fontSize)
                            .SetFontColor(ColorConstants.BLACK)
                            .SetMargin(0)
                            .SetPadding(0)
                            .SetMultipliedLeading(1.15f);

                        layoutCanvas.Add(paragraph);
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