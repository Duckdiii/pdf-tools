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
}
