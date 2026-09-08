using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Hangfire;
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
    /// API Tạo và kích hoạt một Job PDF học thuật mẫu (Tuần 6 Checkpoint):
    /// Chứa tiêu đề, đoạn văn lý thuyết tiếng Anh, 2 công thức toán học và 1 hình ảnh XObject Image
    /// </summary>
    [HttpPost("create-sample-academic")]
    public async Task<IActionResult> CreateSampleAcademicJob()
    {
        var uploadsFolder = Path.Combine(_environment.ContentRootPath, "storage", "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var jobId = Guid.NewGuid();
        var fileName = "sample_academic_optimization.pdf";
        var filePath = Path.Combine(uploadsFolder, $"{jobId}_{fileName}");

        // Tạo ảnh BMP hợp lệ kích thước 260x70 px với dải màu gradient và đường cong hàm loss
        byte[] imageBytes = CreateSampleBmp(260, 70);

        // Tạo file PDF học thuật bằng iText7
        using (var writer = new iText.Kernel.Pdf.PdfWriter(filePath))
        using (var pdf = new iText.Kernel.Pdf.PdfDocument(writer))
        using (var doc = new iText.Layout.Document(pdf))
        {
            // 1. Tiêu đề bài báo học thuật
            doc.Add(new iText.Layout.Element.Paragraph("Deep Neural Network Optimization and Empirical Risk Minimization")
                .SetFontSize(17));

            doc.Add(new iText.Layout.Element.Paragraph("Academic Research Division - Advanced Machine Learning Lab")
                .SetFontSize(10)
                .SetFontColor(iText.Kernel.Colors.ColorConstants.GRAY));

            // 2. Đoạn văn bản lý thuyết dẫn nhập
            doc.Add(new iText.Layout.Element.Paragraph("In deep learning, predictive models minimize an empirical risk objective function over training datasets to optimize model parameters. The regularized objective function is formalized as:")
                .SetFontSize(12));

            // 3. Công thức Toán học 1 (Display Formula với ký hiệu Hy Lạp, tổng Sigma, chỉ số trên/dưới)
            doc.Add(new iText.Layout.Element.Paragraph("L(θ) = (1/N) ∑ [ y_i * log(ŷ_i) + (1 - y_i) * log(1 - ŷ_i) ] + (λ/2) * ||θ||^2    (1)")
                .SetFontSize(13)
                .SetFontColor(iText.Kernel.Colors.ColorConstants.DARK_GRAY)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            // 4. Đoạn văn giải thích tham số công thức
            doc.Add(new iText.Layout.Element.Paragraph("where θ ∈ ℝ^d represents the model parameter vector, λ > 0 denotes the regularization hyperparameter, and N is the number of training samples.")
                .SetFontSize(12));

            // 5. Hình ảnh đồ họa minh họa (XObject Image qua toán tử Do)
            var itextImage = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(imageBytes))
                .SetHorizontalAlignment(iText.Layout.Properties.HorizontalAlignment.CENTER)
                .SetMarginTop(8)
                .SetMarginBottom(8);
            doc.Add(itextImage);

            // 6. Công thức Toán học 2 (Gradient Descent Update Rule)
            doc.Add(new iText.Layout.Element.Paragraph("g_t = ∇_θ L(θ_t),    θ_{t+1} = θ_t - η * g_t    (2)")
                .SetFontSize(13)
                .SetFontColor(iText.Kernel.Colors.ColorConstants.DARK_GRAY)
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

            // 7. Đoạn văn kết luận
            doc.Add(new iText.Layout.Element.Paragraph("This iterative gradient formulation ensures convergence toward local minima under standard smoothness assumptions and learning rate constraints.")
                .SetFontSize(12));
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

        _context.JobStatusHistories.Add(new JobStatusHistory
        {
            Id = Guid.NewGuid(),
            TranslationJobId = job.Id,
            FromStatus = null,
            ToStatus = JobStatus.Pending,
            ChangedAt = DateTime.UtcNow,
            Message = "File PDF học thuật mẫu (có công thức LaTeX & ảnh) đã được tạo và đưa vào hàng đợi xử lý ngầm."
        });

        await _context.SaveChangesAsync();

        // Đẩy vào Hangfire Background Job
        BackgroundJob.Enqueue<ITranslationPipelineService>(svc =>
            svc.ProcessJobPipelineAsync(job.Id, CancellationToken.None));

        return Accepted(new
        {
            jobId = job.Id,
            fileName = job.OriginalFileName,
            sourceLanguage = job.SourceLanguage,
            targetLanguage = job.TargetLanguage,
            status = job.Status.ToString(),
            message = "Tạo file PDF học thuật mẫu thành công. Hangfire đang tự động xử lý bóc tách, phân loại và dịch thuật!",
            statusUrl = $"/api/jobs/{job.Id}/status"
        });
    }

    /// <summary>
    /// Helper sinh byte ảnh BMP chuẩn 24-bit độc lập không phụ thuộc thư viện đồ họa hệ điều hành
    /// </summary>
    private static byte[] CreateSampleBmp(int width, int height)
    {
        int rowSize = (width * 3 + 3) & ~3;
        int imageSize = rowSize * height;
        int fileSize = 54 + imageSize;

        byte[] bmp = new byte[fileSize];

        bmp[0] = 0x42; // 'B'
        bmp[1] = 0x4D; // 'M'
        BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
        BitConverter.GetBytes(54).CopyTo(bmp, 10);

        BitConverter.GetBytes(40).CopyTo(bmp, 14);
        BitConverter.GetBytes(width).CopyTo(bmp, 18);
        BitConverter.GetBytes(height).CopyTo(bmp, 22);
        BitConverter.GetBytes((short)1).CopyTo(bmp, 26);
        BitConverter.GetBytes((short)24).CopyTo(bmp, 28);
        BitConverter.GetBytes(imageSize).CopyTo(bmp, 34);

        for (int y = 0; y < height; y++)
        {
            int rowStart = 54 + y * rowSize;
            for (int x = 0; x < width; x++)
            {
                int p = rowStart + x * 3;
                // Khung viền
                if (x < 2 || x >= width - 2 || y < 2 || y >= height - 2)
                {
                    bmp[p] = 130;
                    bmp[p + 1] = 60;
                    bmp[p + 2] = 15;
                }
                // Đường cong hàm loss y = f(x)
                else if (Math.Abs(y - (int)(height * 0.7 * Math.Exp(-x * 0.02) + 12)) <= 1)
                {
                    bmp[p] = 30;      // B
                    bmp[p + 1] = 50;  // G
                    bmp[p + 2] = 230; // R (Đỏ nổi bật)
                }
                else
                {
                    // Nền xám nhạt dịu mắt
                    bmp[p] = (byte)(245 - x / 8);
                    bmp[p + 1] = (byte)(248 - x / 10);
                    bmp[p + 2] = 252;
                }
            }
        }

        return bmp;
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

        // Ghi nhận lịch sử trạng thái khởi tạo ban đầu
        _context.JobStatusHistories.Add(new JobStatusHistory
        {
            Id = Guid.NewGuid(),
            TranslationJobId = job.Id,
            FromStatus = null,
            ToStatus = JobStatus.Pending,
            ChangedAt = DateTime.UtcNow,
            Message = "File PDF đã được tải lên và đưa vào hàng đợi xử lý ngầm."
        });

        await _context.SaveChangesAsync();

        // Đẩy toàn bộ quy trình pipeline (Extract -> Translate -> Rebuild) vào Hangfire Background Job
        BackgroundJob.Enqueue<ITranslationPipelineService>(svc =>
            svc.ProcessJobPipelineAsync(job.Id, CancellationToken.None));

        // 7. Trả về thông tin Job cho Client ngay lập tức (< 0.5s)
        return Accepted(new
        {
            jobId = job.Id,
            fileName = job.OriginalFileName,
            sourceLanguage = job.SourceLanguage,
            targetLanguage = job.TargetLanguage,
            status = job.Status.ToString(),
            createdAt = job.CreatedAt,
            message = "Tải file lên thành công. Pipeline xử lý ngầm (Extract -> Translate -> Rebuild) đã được kích hoạt!",
            statusUrl = $"/api/jobs/{job.Id}/status"
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
    /// API Polling tra cứu trạng thái tiến độ và lịch sử xử lý ngầm của Job (Background Job)
    /// </summary>
    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetJobStatus(Guid id)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .Include(j => j.StatusHistories)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { message = $"Không tìm thấy Job với mã ID: {id}" });
        }

        var totalBlocks = job.ContentBlocks.Count;
        var translatedBlocks = job.ContentBlocks.Count(b => !string.IsNullOrWhiteSpace(b.TranslatedText));

        // Tính toán phần trăm tiến độ
        int progressPercent = 0;
        switch (job.Status)
        {
            case JobStatus.Pending:
                progressPercent = 5;
                break;
            case JobStatus.Extracting:
                progressPercent = totalBlocks > 0 ? 25 : 15;
                break;
            case JobStatus.Translating:
                if (totalBlocks > 0)
                {
                    progressPercent = 30 + (int)((translatedBlocks / (float)totalBlocks) * 50);
                }
                else
                {
                    progressPercent = 40;
                }
                break;
            case JobStatus.Rebuilding:
                progressPercent = 85;
                break;
            case JobStatus.Completed:
                progressPercent = 100;
                break;
            case JobStatus.Failed:
                progressPercent = 0;
                break;
        }

        var histories = job.StatusHistories
            .OrderBy(h => h.ChangedAt)
            .Select(h => new
            {
                h.Id,
                fromStatus = h.FromStatus?.ToString(),
                toStatus = h.ToStatus.ToString(),
                h.ChangedAt,
                h.Message
            })
            .ToList();

        var latestHistory = histories.LastOrDefault();

        return Ok(new
        {
            jobId = job.Id,
            fileName = job.OriginalFileName,
            status = job.Status.ToString(),
            progressPercent,
            currentStep = latestHistory?.Message ?? $"Đang ở trạng thái {job.Status}",
            totalBlocks,
            translatedBlocks,
            job.ErrorMessage,
            job.CreatedAt,
            job.UpdatedAt,
            history = histories
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
    /// API Xem trực tiếp file PDF gốc trên trình duyệt (cho Side-by-Side Dual Viewer)
    /// </summary>
    [HttpGet("{id:guid}/original-pdf")]
    public async Task<IActionResult> GetOriginalPdf(Guid id)
    {
        var job = await _context.TranslationJobs.FirstOrDefaultAsync(j => j.Id == id);
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

        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

        // TUẦN 6: Gán TranslatedText = OriginalText cho các khối IMAGE và FORMULA_TEXT để bảo toàn
        foreach (var b in job.ContentBlocks.Where(b => b.BlockType != "TEXT"))
        {
            b.TranslatedText = b.OriginalText;
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