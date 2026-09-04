using PdfTranslator.Api.DTOs;

namespace PdfTranslator.Api.Services;

public interface IPdfExtractorService
{
    /// <summary>
    /// Trích xuất toàn bộ các khối văn bản kèm Bounding Box, Font và Số trang từ file PDF
    /// </summary>
    /// <param name="pdfFilePath">Đường dẫn vật lý tới file PDF</param>
    /// <returns>Danh sách các khối văn bản ExtractedBlockDto</returns>
    Task<List<ExtractedBlockDto>> ExtractBlocksAsync(string pdfFilePath);

    /// <summary>
    /// Tạo file PDF mới với các khung viền chữ nhật màu đỏ bao quanh các Text Block để kiểm tra toạ độ
    /// </summary>
    /// <param name="inputPdfPath">Đường dẫn tới file PDF gốc</param>
    /// <param name="blocks">Danh sách các khối văn bản đã trích xuất</param>
    /// <returns>Đường dẫn vật lý tới file PDF debug vừa tạo</returns>
    Task<string> GenerateDebugPdfAsync(string inputPdfPath, List<ExtractedBlockDto> blocks);
}
