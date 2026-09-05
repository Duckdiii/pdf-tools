using PdfTranslator.Api.Models;

namespace PdfTranslator.Api.Services;

public interface IPdfRebuilderService
{
    /// <summary>
    /// Tạo file PDF mới với nội dung tiếng Việt được vẽ đè lên các khối văn bản gốc
    /// </summary>
    /// <param name="originalPdfPath">Đường dẫn file PDF gốc</param>
    /// <param name="blocks">Danh sách các khối văn bản đã dịch kèm toạ độ BoundingBox</param>
    /// <returns>Đường dẫn file PDF tiếng Việt đã tạo</returns>
    Task<string> GenerateTranslatedPdfAsync(string originalPdfPath, List<ContentBlock> blocks);
}