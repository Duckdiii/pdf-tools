namespace PdfTranslator.Api.Services;

public interface ITranslationPipelineService
{
    Task ProcessJobPipelineAsync(Guid jobId, CancellationToken cancellationToken = default);
}
