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

    public JobsController(
        AppDbContext context,
        IWebHostEnvironment environment,
        IPdfExtractorService pdfExtractor)
    {
        _context = context;
        _environment = environment;
        _pdfExtractor = pdfExtractor;
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
}