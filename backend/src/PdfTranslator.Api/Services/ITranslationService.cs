namespace PdfTranslator.Api.Services;

public interface ITranslationService
{
    /// <summary>
    /// Dịch một đoạn văn bản đơn lẻ sang ngôn ngữ đích
    /// </summary>
    /// <param name="text">Đoạn văn bản gốc</param>
    /// <param name="targetLanguage">Mã ngôn ngữ đích (ví dụ: "vi")</param>
    /// <param name="sourceLanguage">Mã ngôn ngữ nguồn (mặc định: "auto")</param>
    /// <returns>Bản dịch tiếng Việt</returns>
    Task<string> TranslateTextAsync(string text, string targetLanguage = "vi", string sourceLanguage = "auto");

    /// <summary>
    /// Dịch một danh sách các khối văn bản trong cùng một request (Batch Translation)
    /// </summary>
    /// <param name="texts">Danh sách các câu/đoạn cần dịch</param>
    /// <param name="targetLanguage">Mã ngôn ngữ đích</param>
    /// <param name="sourceLanguage">Mã ngôn ngữ nguồn</param>
    /// <returns>Danh sách các bản dịch tương ứng theo đúng thứ tự 1-1</returns>
    Task<List<string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "vi", string sourceLanguage = "auto");

    /// <summary>
    /// Dịch một tập hợp các khối văn bản có gắn ID (Key-Value) trong cùng 1 request
    /// </summary>
    /// <param name="items">Từ điển chứa key (block ID) và value (nội dung gốc)</param>
    /// <param name="targetLanguage">Mã ngôn ngữ đích</param>
    /// <param name="sourceLanguage">Mã ngôn ngữ nguồn</param>
    /// <returns>Từ điển chứa key và value đã dịch tương ứng</returns>
    Task<Dictionary<string, string>> TranslateDictionaryAsync(Dictionary<string, string> items, string targetLanguage = "vi", string sourceLanguage = "auto");
}

