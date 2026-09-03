namespace PdfTranslator.Api.DTOs;

public class ExtractedBlockDto
{
    public int PageIndex { get; set; }
    public int OrderIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public string BlockType { get; set; } = "TEXT";
    public BoundingBoxDto BoundingBox { get; set; } = new();
}
