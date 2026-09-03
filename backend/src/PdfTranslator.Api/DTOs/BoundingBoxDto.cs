namespace PdfTranslator.Api.DTOs;

public class BoundingBoxDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string FontName { get; set; } = string.Empty;
    public float FontSize { get; set; }
}
