using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;
using PdfTranslator.Api.DTOs;
using PdfTranslator.Api.Models;
using PdfTranslator.Api.Services;

namespace PdfTranslator.Api.Controllers;

/// <summary>
/// Model DTO chứa dữ liệu gửi lên từ Form
/// </summary>
public class CreateJobRequest
{
    [Required(ErrorMessage = "Vui lòng chọn file PDF.")]
    public IFormFile File { get; set; } = null!;

    public string TargetLanguage { get; set; } = "vi";

    public string SourceLanguage { get; set; } = "auto";
}

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IPdfExtractorService _pdfExtractor;
    private readonly ITranslationService _translationService;
    private readonly IPdfRebuilderService _pdfRebuilder;

    public JobsController(
        AppDbContext context,
        IWebHostEnvironment environment,
        IPdfExtractorService pdfExtractor,
        ITranslationService translationService,
        IPdfRebuilderService pdfRebuilder)
    {
        _context = context;
        _environment = environment;
        _pdfExtractor = pdfExtractor;
        _translationService = translationService;
        _pdfRebuilder = pdfRebuilder;
    }


    /// <summary>
    /// API Tạo và khởi tạo một Job PDF mẫu tiếng Anh 1 trang để kiểm thử dịch thuật (Phase 3 Checkpoint)
    /// </summary>
    [HttpPost("create-sample-en")]
    public async Task<IActionResult> CreateSampleEnglishJob()
    {
        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "storage", "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var jobId = Guid.NewGuid();
        var fileName = "sample_microservices_en.pdf";
        var filePath = Path.Combine(uploadsFolder, $"{jobId}_{fileName}");

        // Tạo file PDF 1 trang tiếng Anh bằng iText7
        using (var writer = new iText.Kernel.Pdf.PdfWriter(filePath))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        using (var doc = new iText.Layout.Document(pdf))
        {
            doc.Add(new iText.Layout.Element.Paragraph("Microservices Architecture Overview")
                .SetFontSize(22));
            doc.Add(new iText.Layout.Element.Paragraph("Microservices are an architectural and organizational approach to software development where software is composed of small independent services.")
                .SetFontSize(14));
            doc.Add(new iText.Layout.Element.Paragraph("These services communicate over well-defined application programming interfaces (APIs).")
                .SetFontSize(14));
            doc.Add(new iText.Layout.Element.Paragraph("Each service is owned by a small, self-contained team that can deploy independently.")
                .SetFontSize(14));
            doc.Add(new iText.Layout.Element.Paragraph("Microservice architectures make applications easier to scale and faster to develop, enabling innovation and accelerating time-to-market for new features.")
                .SetFontSize(14));
        }

        var job = new TranslationJob
        {
            Id = jobId,
            OriginalFileName = fileName,
            StoredFilePath = filePath,
            SourceLanguage = "en",
            TargetLanguage = "vi",
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.TranslationJobs.Add(job);
        await _context.SaveChangesAsync();

        // Bóc tách nội dung ngay
        var extractedBlocks = await _pdfExtractor.ExtractBlocksAsync(filePath);
        foreach (var b in extractedBlocks)
        {
            var contentBlock = new ContentBlock
            {
                Id = Guid.NewGuid(),
                TranslationJobId = job.Id,
                PageIndex = b.PageIndex,
                OrderIndex = b.OrderIndex,
                OriginalText = b.Text.Replace("\0", string.Empty),
                BlockType = b.BlockType,
                BoundingBoxJson = JsonSerializer.Serialize(b.BoundingBox).Replace("\0", string.Empty)
            };
            _context.ContentBlocks.Add(contentBlock);
        }

        job.Status = JobStatus.Extracting;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            jobId = job.Id,
            fileName = job.OriginalFileName,
            status = job.Status.ToString(),
            totalBlocks = extractedBlocks.Count,
            blocks = extractedBlocks.Select(b => b.Text).ToList(),
            message = "Tạo và bóc tách file PDF mẫu tiếng Anh thành công!"
        });
    }

    /// <summary>
    /// API Upload file PDF và tạo Job dịch mới
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateJob([FromForm] CreateJobRequest request)

    {
        var file = request.File;

        // 1. Kiểm tra file hợp lệ
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn một file PDF hợp lệ." });
        }

        // 2. Kiểm tra định dạng file (.pdf)
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".pdf")
        {
            return BadRequest(new { message = "Chỉ chấp nhận file có định dạng .pdf." });
        }

        // 3. Đảm bảo thư mục lưu trữ 'storage/uploads/' tồn tại
        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "storage", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // 4. Tạo tên file duy nhất tránh trùng lặp
        var jobId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);
        var uniqueFileName = $"{jobId}_{safeFileName}";
        var destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

        // 5. Lưu file vật lý xuống ổ cứng
        using (var stream = new FileStream(destinationPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 6. Tạo record TranslationJob trong Database với trạng thái Pending
        var job = new TranslationJob
        {
            Id = jobId,
            OriginalFileName = safeFileName,
            StoredFilePath = destinationPath,
            SourceLanguage = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "auto" : request.SourceLanguage,
            TargetLanguage = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "vi" : request.TargetLanguage,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.TranslationJobs.Add(job);
        await _context.SaveChangesAsync();

        // 7. Trả về thông tin Job cho Client
        return Ok(new
        {
            jobId = job.Id,
            fileName = job.OriginalFileName,
            sourceLanguage = job.SourceLanguage,
            targetLanguage = job.TargetLanguage,
            status = job.Status.ToString(),
            createdAt = job.CreatedAt,
            message = "Tải lên file thành công. Job đang ở trạng thái chờ xử lý."
        });
    }

    /// <summary>
    /// API Tra cứu trạng thái và thông tin của Job theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJobById(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        return Ok(new
        {
            job.Id,
            job.OriginalFileName,
            job.SourceLanguage,
            job.TargetLanguage,
            Status = job.Status.ToString(),
            job.ErrorMessage,
            job.CreatedAt,
            job.UpdatedAt,
            TotalBlocks = job.ContentBlocks.Count
        });
    }

    /// <summary>
    /// API Bóc tách nội dung PDF kèm Bounding Box, Font và Số trang (Tuần 2)
    /// </summary>
    [HttpPost("{id:guid}/extract")]
    public async Task<IActionResult> ExtractJobContent(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var filePath = job.StoredFilePath;
        if (!System.IO.File.Exists(filePath))
        {
            var fallback = Path.Combine(_environment.ContentRootPath, "storage", "uploads", Path.GetFileName(filePath));
            if (System.IO.File.Exists(fallback))
            {
                filePath = fallback;
                job.StoredFilePath = fallback;
            }
            else
            {
                return BadRequest(new { message = $"File PDF vật lý không tồn tại tại: {job.StoredFilePath}" });
            }
        }

        // Cập nhật trạng thái sang Extracting
        job.Status = JobStatus.Extracting;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        try
        {
            var extractedBlocks = await _pdfExtractor.ExtractBlocksAsync(filePath);

            // In ra console kiểm tra checkpoint theo đúng yêu cầu Tuần 2
            Console.WriteLine($"\n==================== CHECKPOINT TUẦN 2: EXTRACT JOB {job.Id} ====================");
            Console.WriteLine($"Tên file: {job.OriginalFileName} | Tổng số block trích xuất: {extractedBlocks.Count}");
            foreach (var b in extractedBlocks)
            {
                Console.WriteLine($"[Trang {b.PageIndex}][Thứ tự {b.OrderIndex:D2}] \"{b.Text}\"");
                Console.WriteLine($"   └─ BoundingBox: X={b.BoundingBox.X:F1}, Y={b.BoundingBox.Y:F1}, W={b.BoundingBox.Width:F1}, H={b.BoundingBox.Height:F1} | Font: {b.BoundingBox.FontName} ({b.BoundingBox.FontSize}pt)");
            }
            Console.WriteLine("===================================================================================\n");

            // Map kết quả extract vào ContentBlock (blockType = TEXT), lưu DB theo yêu cầu Tuần 2
            if (job.ContentBlocks.Count > 0)
            {
                _context.ContentBlocks.RemoveRange(job.ContentBlocks);
            }

            foreach (var b in extractedBlocks)
            {
                var contentBlock = new ContentBlock
                {
                    Id = Guid.NewGuid(),
                    TranslationJobId = job.Id,
                    PageIndex = b.PageIndex,
                    OrderIndex = b.OrderIndex,
                    OriginalText = b.Text.Replace("\0", string.Empty),
                    BlockType = b.BlockType,
                    BoundingBoxJson = JsonSerializer.Serialize(b.BoundingBox).Replace("\0", string.Empty)
                };
                _context.ContentBlocks.Add(contentBlock);
            }

            // Cập nhật thời gian và lưu vào database
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                jobId = job.Id,
                fileName = job.OriginalFileName,
                status = job.Status.ToString(),
                totalBlocks = extractedBlocks.Count,
                blocks = extractedBlocks
            });
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return StatusCode(500, new { message = "Lỗi khi trích xuất nội dung PDF.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Truy vấn danh sách ContentBlocks đã lưu trong Database của một Job
    /// </summary>
    [HttpGet("{id:guid}/blocks")]
    public async Task<IActionResult> GetJobBlocks(Guid id)
    {
        var jobExists = await _context.TranslationJobs.AnyAsync(j => j.Id == id);
        if (!jobExists)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var blocks = await _context.ContentBlocks
            .Where(b => b.TranslationJobId == id)
            .OrderBy(b => b.PageIndex)
            .ThenBy(b => b.OrderIndex)
            .ToListAsync();

        return Ok(new
        {
            jobId = id,
            totalBlocks = blocks.Count,
            blocks = blocks.Select(b => new
            {
                b.Id,
                b.PageIndex,
                b.OrderIndex,
                b.BlockType,
                b.OriginalText,
                b.TranslatedText,
                TranslatedContent = b.TranslatedText,
                BoundingBox = string.IsNullOrEmpty(b.BoundingBoxJson)
                    ? null
                    : JsonSerializer.Deserialize<BoundingBoxDto>(b.BoundingBoxJson)
            })
        });
    }

    /// <summary>
    /// API Xem hoặc Tải file PDF Debug có vẽ khung đỏ bao quanh các Text Block
    /// </summary>
    [HttpGet("{id:guid}/debug-pdf")]
    public async Task<IActionResult> GetDebugPdf(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var filePath = job.StoredFilePath;
        if (!System.IO.File.Exists(filePath))
        {
            var fallback = Path.Combine(_environment.ContentRootPath, "storage", "uploads", Path.GetFileName(filePath));
            if (System.IO.File.Exists(fallback))
            {
                filePath = fallback;
                job.StoredFilePath = fallback;
            }
            else
            {
                return BadRequest(new { message = $"File PDF gốc không tồn tại tại: {job.StoredFilePath}" });
            }
        }

        // Lấy danh sách blocks: nếu trong DB đã có thì lấy từ DB, nếu chưa có thì trích xuất ngay
        List<ExtractedBlockDto> blocksToDraw;
        if (job.ContentBlocks.Count > 0)
        {
            blocksToDraw = job.ContentBlocks.Select(b => new ExtractedBlockDto
            {
                PageIndex = b.PageIndex,
                OrderIndex = b.OrderIndex,
                Text = b.OriginalText,
                BlockType = b.BlockType,
                BoundingBox = string.IsNullOrEmpty(b.BoundingBoxJson)
                    ? new BoundingBoxDto()
                    : JsonSerializer.Deserialize<BoundingBoxDto>(b.BoundingBoxJson) ?? new BoundingBoxDto()
            }).ToList();
        }
        else
        {
            blocksToDraw = await _pdfExtractor.ExtractBlocksAsync(filePath);
        }

        // Tạo file debug PDF có khung đỏ
        var debugPdfPath = await _pdfExtractor.GenerateDebugPdfAsync(filePath, blocksToDraw);

        // Trả về file PDF để trình duyệt mở xem trực tiếp
        var fileStream = new FileStream(debugPdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(fileStream, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// API Xem trực tiếp file PDF đã dịch tiếng Việt trên trình duyệt (Phase 4)
    /// </summary>
    [HttpGet("{id:guid}/translated-pdf")]
    public async Task<IActionResult> GetTranslatedPdf(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var filePath = job.StoredFilePath;
        if (!System.IO.File.Exists(filePath))
        {
            var fallback = Path.Combine(_environment.ContentRootPath, "storage", "uploads", Path.GetFileName(filePath));
            if (System.IO.File.Exists(fallback))
            {
                filePath = fallback;
                job.StoredFilePath = fallback;
            }
            else
            {
                return BadRequest(new { message = $"File PDF gốc không tồn tại tại: {job.StoredFilePath}" });
            }
        }

        if (job.ContentBlocks == null || job.ContentBlocks.Count == 0)
        {
            return BadRequest(new { message = "Job chưa có nội dung bóc tách hoặc bản dịch. Vui lòng bóc tách và dịch trước." });
        }

        try
        {
            var translatedPdfPath = await _pdfRebuilder.GenerateTranslatedPdfAsync(filePath, job.ContentBlocks.ToList());

            var fileStream = new FileStream(translatedPdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(fileStream, "application/pdf", enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tái tạo file PDF tiếng Việt.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Tải file PDF đã dịch tiếng Việt về máy tính (Phase 4)
    /// </summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadTranslatedPdf(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var filePath = job.StoredFilePath;
        if (!System.IO.File.Exists(filePath))
        {
            var fallback = Path.Combine(_environment.ContentRootPath, "storage", "uploads", Path.GetFileName(filePath));
            if (System.IO.File.Exists(fallback))
            {
                filePath = fallback;
                job.StoredFilePath = fallback;
            }
            else
            {
                return BadRequest(new { message = $"File PDF gốc không tồn tại tại: {job.StoredFilePath}" });
            }
        }

        if (job.ContentBlocks == null || job.ContentBlocks.Count == 0)
        {
            return BadRequest(new { message = "Job chưa có nội dung bóc tách hoặc bản dịch. Vui lòng bóc tách và dịch trước." });
        }

        try
        {
            var translatedPdfPath = await _pdfRebuilder.GenerateTranslatedPdfAsync(filePath, job.ContentBlocks.ToList());

            var fileStream = new FileStream(translatedPdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var downloadFileName = $"{Path.GetFileNameWithoutExtension(job.OriginalFileName)}_translated_vi.pdf";
            return File(fileStream, "application/pdf", downloadFileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi tải file PDF tiếng Việt.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Test thử nghiệm dịch vụ dịch thuật (Phase 3)
    /// </summary>

    [HttpPost("test-translate")]
    public async Task<IActionResult> TestTranslate([FromBody] TestTranslateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "Vui lòng cung cấp đoạn văn bản cần dịch." });
        }

        try
        {
            var translated = await _translationService.TranslateTextAsync(
                request.Text,
                request.TargetLanguage ?? "vi",
                request.SourceLanguage ?? "auto");

            return Ok(new
            {
                originalText = request.Text,
                sourceLanguage = request.SourceLanguage ?? "auto",
                targetLanguage = request.TargetLanguage ?? "vi",
                translatedText = translated
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi gọi dịch vụ dịch thuật.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Test thử nghiệm dịch vụ Dictionary Batch Translation (Phase 3)
    /// </summary>
    [HttpPost("test-translate-batch")]
    public async Task<IActionResult> TestTranslateBatch([FromBody] TestTranslateBatchRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng cung cấp danh sách items cần dịch." });
        }

        try
        {
            var translated = await _translationService.TranslateDictionaryAsync(
                request.Items,
                request.TargetLanguage ?? "vi",
                request.SourceLanguage ?? "auto");

            return Ok(new
            {
                targetLanguage = request.TargetLanguage ?? "vi",
                sourceLanguage = request.SourceLanguage ?? "auto",
                totalItems = request.Items.Count,
                translatedItems = translated
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi gọi dịch vụ dịch thuật batch.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Dịch nội dung PDF của một Job theo từng trang (Batch Dictionary Translation)
    /// Hỗ trợ tham số fromPage và toPage để chọn khoảng trang cần dịch (mặc định dịch toàn bộ)
    /// </summary>
    [HttpPost("{id:guid}/translate")]
    public async Task<IActionResult> TranslateJob(Guid id, [FromQuery] int? fromPage = null, [FromQuery] int? toPage = null)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var textBlocks = job.ContentBlocks
            .Where(b => b.BlockType == "TEXT" && !string.IsNullOrWhiteSpace(b.OriginalText))
            .OrderBy(b => b.PageIndex)
            .ThenBy(b => b.OrderIndex)
            .ToList();

        if (textBlocks.Count == 0)
        {
            return BadRequest(new { message = "Job chưa có khối văn bản nào được bóc tách. Vui lòng gọi API /extract trước." });
        }

        job.Status = JobStatus.Translating;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        try
        {
            var allPages = textBlocks.GroupBy(b => b.PageIndex).OrderBy(g => g.Key).ToList();
            var pages = allPages
                .Where(g => (!fromPage.HasValue || g.Key >= fromPage.Value) && (!toPage.HasValue || g.Key <= toPage.Value))
                .ToList();

            if (pages.Count == 0)
            {
                return BadRequest(new { message = $"Không tìm thấy trang nào trong khoảng từ {fromPage} đến {toPage}." });
            }

            int translatedCount = 0;

            Console.WriteLine($"\n==================== BẮT ĐẦU DỊCH JOB {job.Id} ====================");
            Console.WriteLine($"Tên file: {job.OriginalFileName} | Ngôn ngữ đích: {job.TargetLanguage}");
            Console.WriteLine($"Tổng số trang dịch đợt này: {pages.Count}/{allPages.Count} | Tổng số text block: {pages.Sum(p => p.Count())}");

            int pageCounter = 0;
            foreach (var pageGroup in pages)
            {
                pageCounter++;
                var pageIndex = pageGroup.Key;
                var pageBlocks = pageGroup.ToList();

                Console.WriteLine($"--> [{pageCounter}/{pages.Count}] Đang dịch Trang {pageIndex} ({pageBlocks.Count} blocks)...");


                // Gom các block trong trang thành Dictionary [ID] -> [OriginalText]
                var pageDict = new Dictionary<string, string>();
                foreach (var b in pageBlocks)
                {
                    pageDict[b.Id.ToString()] = b.OriginalText;
                }

                // Gửi 1 request duy nhất cho cả trang
                var translatedDict = await _translationService.TranslateDictionaryAsync(
                    pageDict,
                    job.TargetLanguage,
                    job.SourceLanguage);

                // Cập nhật bản dịch ngược lại vào từng block
                foreach (var b in pageBlocks)
                {
                    var key = b.Id.ToString();
                    if (translatedDict.TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated))
                    {
                        b.TranslatedText = translated;
                    }
                    else
                    {
                        b.TranslatedText = b.OriginalText; // Fallback nếu thiếu
                    }
                    translatedCount++;
                }

                // Lưu lũy tiến ngay sau khi dịch xong mỗi trang vào Database
                job.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                Console.WriteLine($"   Trang {pageIndex} ({pageBlocks.Count} blocks) đã lưu thành công vào Database.");

                // Khoảng nghỉ nhẹ giữa các trang để chống nghẽn Rate Limit
                if (pageCounter < pages.Count)
                {
                    await Task.Delay(2000);
                }
            }


            job.Status = JobStatus.Completed;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            Console.WriteLine($" Hoàn thành dịch Job {job.Id}: {translatedCount}/{textBlocks.Count} blocks đã lưu vào Database.");
            Console.WriteLine("======================================================================\n");

            return Ok(new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                totalPages = pages.Count,
                totalTranslatedBlocks = translatedCount,
                message = "Dịch thành công toàn bộ tài liệu PDF và đã lưu vào cơ sở dữ liệu."
            });
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return StatusCode(500, new { message = "Lỗi trong quá trình dịch thuật.", error = ex.Message });
        }
    }

    /// <summary>
    /// API Chỉnh sửa thủ công bản dịch của một ContentBlock cụ thể (Lưu translatedContent vào DB)
    /// </summary>
    [HttpPut("{id:guid}/blocks/{blockId:guid}")]
    public async Task<IActionResult> UpdateBlockTranslation(Guid id, Guid blockId, [FromBody] UpdateBlockTranslationRequest request)
    {
        var block = await _context.ContentBlocks
            .FirstOrDefaultAsync(b => b.Id == blockId && b.TranslationJobId == id);

        if (block == null)
        {
            return NotFound(new { message = $"Không tìm thấy Block {blockId} trong Job {id}." });
        }

        var newContent = request.TranslatedContent ?? request.TranslatedText;
        if (newContent != null)
        {
            block.TranslatedText = newContent;
        }

        var job = await _context.TranslationJobs.FirstOrDefaultAsync(j => j.Id == id);
        if (job != null)
        {
            job.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            blockId = block.Id,
            jobId = block.TranslationJobId,
            pageIndex = block.PageIndex,
            orderIndex = block.OrderIndex,
            originalText = block.OriginalText,
            translatedText = block.TranslatedText,
            translatedContent = block.TranslatedText,
            message = "Cập nhật bản dịch cho block thành công và đã lưu vào cơ sở dữ liệu."
        });
    }
}

public class TestTranslateRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
    public string? TargetLanguage { get; set; } = "vi";
    public string? SourceLanguage { get; set; } = "auto";
}

public class TestTranslateBatchRequest
{
    [Required]
    public Dictionary<string, string> Items { get; set; } = new();
    public string? TargetLanguage { get; set; } = "vi";
    public string? SourceLanguage { get; set; } = "auto";
}

public class UpdateBlockTranslationRequest
{
    public string? TranslatedContent { get; set; }
    public string? TranslatedText { get; set; }
}