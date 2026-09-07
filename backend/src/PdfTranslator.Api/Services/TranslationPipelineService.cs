using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PdfTranslator.Api.Data;
using PdfTranslator.Api.Models;

namespace PdfTranslator.Api.Services;

public class TranslationPipelineService : ITranslationPipelineService
{
    private readonly AppDbContext _context;
    private readonly IPdfExtractorService _pdfExtractor;
    private readonly ITranslationService _translationService;
    private readonly IPdfRebuilderService _pdfRebuilder;
    private readonly ILogger<TranslationPipelineService> _logger;

    public TranslationPipelineService(
        AppDbContext context,
        IPdfExtractorService pdfExtractor,
        ITranslationService translationService,
        IPdfRebuilderService pdfRebuilder,
        ILogger<TranslationPipelineService> logger)
    {
        _context = context;
        _pdfExtractor = pdfExtractor;
        _translationService = translationService;
        _pdfRebuilder = pdfRebuilder;
        _logger = logger;
    }

    public async Task ProcessJobPipelineAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _context.TranslationJobs
            .Include(j => j.ContentBlocks)
            .Include(j => j.StatusHistories)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogWarning("Không tìm thấy Job {JobId} trong cơ sở dữ liệu để thực thi pipeline.", jobId);
            return;
        }

        _logger.LogInformation(">>> [Hangfire Worker] Bắt đầu thực thi pipeline xử lý ngầm cho Job {JobId} ({FileName})",
            job.Id, job.OriginalFileName);

        try
        {
            var filePath = job.StoredFilePath;
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File PDF vật lý không tồn tại tại: {job.StoredFilePath}");
            }

            // -------------------------------------------------------------
            // BƯỚC 1: EXTRACTING - Bóc tách cấu trúc và tọa độ văn bản
            // -------------------------------------------------------------
            await UpdateJobStatusAsync(job, JobStatus.Extracting, "Đang bóc tách các khối văn bản và tọa độ Bounding Box...", cancellationToken);

            var extractedBlocks = await _pdfExtractor.ExtractBlocksAsync(filePath);

            if (job.ContentBlocks.Count > 0)
            {
                _context.ContentBlocks.RemoveRange(job.ContentBlocks);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var newBlocks = new List<ContentBlock>();
            foreach (var b in extractedBlocks)
            {
                var block = new ContentBlock
                {
                    Id = Guid.NewGuid(),
                    TranslationJobId = job.Id,
                    PageIndex = b.PageIndex,
                    OrderIndex = b.OrderIndex,
                    OriginalText = b.Text.Replace("\0", string.Empty),
                    BlockType = b.BlockType,
                    BoundingBoxJson = JsonSerializer.Serialize(b.BoundingBox).Replace("\0", string.Empty)
                };
                _context.ContentBlocks.Add(block);
                newBlocks.Add(block);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await UpdateJobStatusAsync(job, JobStatus.Extracting, $"Đã bóc tách thành công {extractedBlocks.Count} khối văn bản.", cancellationToken);

            // -------------------------------------------------------------
            // BƯỚC 2: TRANSLATING - Dịch theo Batch từng trang bằng AI
            // -------------------------------------------------------------
            var textBlocks = newBlocks
                .Where(b => b.BlockType == "TEXT" && !string.IsNullOrWhiteSpace(b.OriginalText))
                .OrderBy(b => b.PageIndex)
                .ThenBy(b => b.OrderIndex)
                .ToList();

            if (textBlocks.Count == 0)
            {
                throw new InvalidOperationException("Không tìm thấy khối văn bản nào để dịch.");
            }

            var pageGroups = textBlocks.GroupBy(b => b.PageIndex).OrderBy(g => g.Key).ToList();
            await UpdateJobStatusAsync(job, JobStatus.Translating, $"Bắt đầu dịch {pageGroups.Count} trang ({textBlocks.Count} blocks) bằng AI...", cancellationToken);

            int pageCounter = 0;
            foreach (var pageGroup in pageGroups)
            {
                pageCounter++;
                var pageIndex = pageGroup.Key;
                var pageBlocks = pageGroup.ToList();

                var pageDict = new Dictionary<string, string>();
                foreach (var b in pageBlocks)
                {
                    pageDict[b.Id.ToString()] = b.OriginalText;
                }

                var translatedDict = await _translationService.TranslateDictionaryAsync(
                    pageDict,
                    job.TargetLanguage,
                    job.SourceLanguage);

                foreach (var b in pageBlocks)
                {
                    var key = b.Id.ToString();
                    if (translatedDict.TryGetValue(key, out var translated) && !string.IsNullOrWhiteSpace(translated))
                    {
                        b.TranslatedText = translated;
                    }
                    else
                    {
                        b.TranslatedText = b.OriginalText;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await UpdateJobStatusAsync(job, JobStatus.Translating,
                    $"Đã dịch xong Trang {pageIndex} ({pageCounter}/{pageGroups.Count} trang).", cancellationToken);

                if (pageCounter < pageGroups.Count)
                {
                    await Task.Delay(1500, cancellationToken);
                }
            }

            // -------------------------------------------------------------
            // BƯỚC 3: REBUILDING - Tái tạo file PDF tiếng Việt giữ nguyên layout
            // -------------------------------------------------------------
            await UpdateJobStatusAsync(job, JobStatus.Rebuilding, "Đang tái tạo và xuất bản file PDF Tiếng Việt chuẩn xác từng pixel...", cancellationToken);

            var translatedPdfPath = await _pdfRebuilder.GenerateTranslatedPdfAsync(filePath, newBlocks);

            await UpdateJobStatusAsync(job, JobStatus.Rebuilding,
                $"File PDF tiếng Việt đã được tạo tại {Path.GetFileName(translatedPdfPath)}.", cancellationToken);

            // -------------------------------------------------------------
            // BƯỚC 4: COMPLETED - Hoàn tất
            // -------------------------------------------------------------
            await UpdateJobStatusAsync(job, JobStatus.Completed, "Hoàn tất toàn bộ quy trình dịch và xuất bản PDF tiếng Việt!", cancellationToken);

            _logger.LogInformation(">>> [Hangfire Worker] Job {JobId} hoàn thành 100%!", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra khi thực thi background pipeline cho Job {JobId}", jobId);
            try
            {
                _context.ChangeTracker.Clear();
                var failedJob = await _context.TranslationJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
                if (failedJob != null)
                {
                    failedJob.ErrorMessage = ex.Message;
                    await UpdateJobStatusAsync(failedJob, JobStatus.Failed, $"Lỗi xử lý: {ex.Message}", cancellationToken);
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Không thể cập nhật trạng thái Failed cho Job {JobId}", jobId);
            }
        }
    }

    private async Task UpdateJobStatusAsync(
        TranslationJob job,
        JobStatus newStatus,
        string? message,
        CancellationToken cancellationToken)
    {
        var oldStatus = job.Status;
        job.Status = newStatus;
        job.UpdatedAt = DateTime.UtcNow;

        var history = new JobStatusHistory
        {
            Id = Guid.NewGuid(),
            TranslationJobId = job.Id,
            FromStatus = oldStatus,
            ToStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            Message = message
        };

        _context.JobStatusHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Job {JobId}: [{OldStatus} -> {NewStatus}] {Message}",
            job.Id, oldStatus, newStatus, message);
    }
}
