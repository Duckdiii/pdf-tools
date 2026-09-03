using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PdfTranslator.Api.Models;

public class ContentBlock
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key tới TranslationJob
    [Required]
    public Guid TranslationJobId { get; set; }

    [ForeignKey(nameof(TranslationJobId))]
    public TranslationJob? TranslationJob { get; set; }

    public int PageIndex { get; set; }

    public int OrderIndex { get; set; }

    [Required]
    [MaxLength(50)]
    public string BlockType { get; set; } = "TEXT";

    [Required]
    public string OriginalText { get; set; } = string.Empty;

    public string? TranslatedText { get; set; }

    // JSON lưu metadata tọa độ / bounding box / font styles
    public string? BoundingBoxJson { get; set; }
}
