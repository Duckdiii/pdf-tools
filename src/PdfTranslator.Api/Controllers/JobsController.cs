using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;
using PdfTranslator.Api.Models;

namespace PdfTranslator.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    // Inject AppDbContext (thao tác DB) và IWebHostEnvironment (lấy đường dẫn thư mục)
    public JobsController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    /// <summary>
    /// API Upload file PDF và tạo Job dịch mới
    /// </summary>
    /// <param name="file">File PDF cần dịch</param>
    /// <param name="targetLanguage">Ngôn ngữ đích (mặc định: vi)</param>
    /// <param name="sourceLanguage">Ngôn ngữ nguồn (mặc định: auto)</param>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateJob(
        [FromForm] IFormFile? file,
        [FromForm] string targetLanguage = "vi",
        [FromForm] string sourceLanguage = "auto")
    {
        // 1. Kiểm tra file có được gửi lên không
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Vui lòng chọn một file PDF hợp lệ." });
        }

        // 2. Kiểm tra định dạng file (chỉ cho phép .pdf)
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

        // 4. Tạo tên file độc nhất (UUID + Tên gốc) để tránh bị ghi đè file khi trùng tên
        var jobId = Guid.NewGuid();
        var safeFileName = Path.GetFileName(file.FileName);
        var uniqueFileName = $"{jobId}_{safeFileName}";
        var destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

        // 5. Ghi file vật lý xuống ổ cứng
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
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.TranslationJobs.Add(job);
        await _context.SaveChangesAsync();

        // 7. Trả về thông tin Job và mã JobId cho Client
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
}