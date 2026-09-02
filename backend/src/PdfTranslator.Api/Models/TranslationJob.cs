using System.ComponentModel.DataAnnotations;

namespace PdfTranslator.Api.Models;

public class TranslationJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    public string StoredFilePath { get; set; } = string.Empty;

    [MaxLength(10)]
    public string SourceLanguage { get; set; } = "auto";

    [Required]
    [MaxLength(10)]
    public string TargetLanguage { get; set; } = "vi";

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation property: 1 TranslationJob có nhiều ContentBlock
    public ICollection<ContentBlock> ContentBlocks { get; set; } = new List<ContentBlock>();
}
