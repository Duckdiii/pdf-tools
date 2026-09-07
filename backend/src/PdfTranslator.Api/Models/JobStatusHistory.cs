using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PdfTranslator.Api.Models;

public class JobStatusHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid TranslationJobId { get; set; }

    [ForeignKey(nameof(TranslationJobId))]
    public TranslationJob? TranslationJob { get; set; }

    public JobStatus? FromStatus { get; set; }

    [Required]
    public JobStatus ToStatus { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? Message { get; set; }
}
