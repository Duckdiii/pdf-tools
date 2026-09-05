using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PdfTranslator.Api.Services;

public class GeminiTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiTranslationService> _logger;
    private readonly IConfiguration _configuration;
    private const string DefaultModel = "gemini-3.6-flash";

    public GeminiTranslationService(
        HttpClient httpClient,
        ILogger<GeminiTranslationService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Dịch một đoạn văn bản đơn lẻ sang ngôn ngữ đích bằng Gemini API
    /// </summary>
    public async Task<string> TranslateTextAsync(string text, string targetLanguage = "vi", string sourceLanguage = "auto")
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var apiKey = GetApiKey();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{DefaultModel}:generateContent?key={apiKey}";
        var systemPrompt = BuildSystemPrompt(targetLanguage);

        var requestPayload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { parts = new[] { new { text = text } } }
            },
            generationConfig = new
            {
                temperature = 0.3
            }
        };

        var response = await SendWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(requestPayload);
            return await _httpClient.SendAsync(request);
        });

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        try
        {
            var content = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return content?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi phân tích JSON phản hồi từ Gemini: {Json}", json);
            throw new InvalidOperationException($"Phản hồi từ Gemini không đúng định dạng: {ex.Message}");
        }
    }

    /// <summary>
    /// Dịch một danh sách khối văn bản trong cùng một request (Batch Translation)
    /// </summary>
    public async Task<List<string>> TranslateBatchAsync(List<string> texts, string targetLanguage = "vi", string sourceLanguage = "auto")
    {
        if (texts == null || texts.Count == 0) return new List<string>();

        var apiKey = GetApiKey();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{DefaultModel}:generateContent?key={apiKey}";
        var systemPrompt = BuildBatchSystemPrompt(targetLanguage);

        var requestPayload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { parts = new[] { new { text = JsonSerializer.Serialize(new { texts }) } } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                response_mime_type = "application/json"
            }
        };

        var response = await SendWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(requestPayload);
            return await _httpClient.SendAsync(request);
        });

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var rawContent = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(rawContent))
        {
            throw new InvalidOperationException("Gemini không trả về nội dung bản dịch batch.");
        }

        var cleanJson = CleanJsonString(rawContent);
        using var innerDoc = JsonDocument.Parse(cleanJson);
        
        var results = new List<string>();
        if (innerDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in innerDoc.RootElement.EnumerateArray())
            {
                results.Add(item.GetString() ?? string.Empty);
            }
        }
        else if (innerDoc.RootElement.TryGetProperty("translations", out var translationsArray))
        {
            foreach (var item in translationsArray.EnumerateArray())
            {
                results.Add(item.GetString() ?? string.Empty);
            }
        }

        if (results.Count != texts.Count)
        {
            _logger.LogWarning("Số lượng bản dịch ({ResultCount}) không khớp với số lượng đầu vào ({InputCount})",
                results.Count, texts.Count);
            
            // Pad hoặc cắt để khớp 1-1
            while (results.Count < texts.Count)
            {
                results.Add(texts[results.Count]); // Fallback câu gốc
            }
        }

        return results;
    }

    /// <summary>
    /// Dịch một từ điển các khối văn bản có gắn ID (Key-Value) trong cùng 1 request
    /// </summary>
    public async Task<Dictionary<string, string>> TranslateDictionaryAsync(
        Dictionary<string, string> items, 
        string targetLanguage = "vi", 
        string sourceLanguage = "auto")
    {
        if (items == null || items.Count == 0) return new Dictionary<string, string>();

        var apiKey = GetApiKey();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{DefaultModel}:generateContent?key={apiKey}";
        var systemPrompt = BuildDictionarySystemPrompt(targetLanguage);

        var requestPayload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { parts = new[] { new { text = JsonSerializer.Serialize(items) } } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                response_mime_type = "application/json"
            }
        };

        var response = await SendWithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(requestPayload);
            return await _httpClient.SendAsync(request);
        });

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var rawContent = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(rawContent))
        {
            throw new InvalidOperationException("Gemini không trả về nội dung bản dịch dictionary.");
        }

        var cleanJson = CleanJsonString(rawContent);
        using var innerDoc = JsonDocument.Parse(cleanJson);

        var results = new Dictionary<string, string>();
        if (innerDoc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in innerDoc.RootElement.EnumerateObject())
            {
                results[prop.Name] = prop.Value.GetString() ?? string.Empty;
            }
        }

        // Đảm bảo mọi key trong input đều có mặt trong kết quả (fallback nếu Gemini bỏ sót key)
        foreach (var kvp in items)
        {
            if (!results.ContainsKey(kvp.Key) || string.IsNullOrWhiteSpace(results[kvp.Key]))
            {
                _logger.LogWarning("Gemini bỏ sót key {Key}, sử dụng nội dung gốc làm fallback.", kvp.Key);
                results[kvp.Key] = kvp.Value;
            }
        }

        return results;
    }

    /// <summary>
    /// Cơ chế gọi HTTP với Exponential Backoff Retry khi gặp lỗi Rate Limit (429) hoặc 5xx Server Error
    /// </summary>
    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<Task<HttpResponseMessage>> sendRequest)
    {
        const int maxRetries = 4;
        int delayMs = 2000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            var response = await sendRequest();

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            // Xử lý khi gặp Rate Limit (429) hoặc lỗi máy chủ (5xx)
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                if (attempt == maxRetries)
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Gemini API gặp lỗi {response.StatusCode} sau {maxRetries} lần thử lại: {errBody}");
                }

                int waitMs = delayMs;
                if (response.Headers.RetryAfter?.Delta.HasValue == true)
                {
                    waitMs = (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds;
                }
                else if (response.Headers.RetryAfter?.Date.HasValue == true)
                {
                    waitMs = (int)(response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalMilliseconds;
                }

                waitMs = Math.Max(1500, waitMs);
                _logger.LogWarning(
                    "Gemini trả về mã {StatusCode} (Rate Limit / Quá tải). Đang chờ {WaitSeconds:F1}s trước khi thử lại lần {Attempt}/{MaxRetries}...",
                    response.StatusCode, waitMs / 1000.0, attempt, maxRetries);

                await Task.Delay(waitMs);
                delayMs *= 2;
                continue;
            }

            // Nếu gặp lỗi khác (ví dụ: 400 Bad Request, 403 Forbidden do sai API key) thì dừng ngay
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Lỗi gọi Gemini API [{(int)response.StatusCode} {response.StatusCode}]: {errorContent}");
        }

        throw new HttpRequestException("Đã vượt quá số lần thử lại tối đa khi gọi Gemini API.");
    }

    private string GetApiKey()
    {
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _configuration["GEMINI_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình GEMINI_API_KEY trong file backend/.env hoặc biến môi trường.");
        }
        return apiKey;
    }

    private static string BuildSystemPrompt(string targetLanguage)
    {
        return $"You are a professional technical translator specializing in software engineering and computer science. " +
               $"Translate the user text into {targetLanguage} naturally, accurately, and concisely. " +
               $"IMPORTANT RULES: " +
               $"1. Keep all code identifiers, class names, method names, annotations, and technical terms (e.g. Facade, Spring Boot, @Service, InventoryService, Controller, Interface, Class, Method) in their original English/code form. " +
               $"2. Do not add any conversational filler, notes, or introductory text (like 'Here is the translation:'). " +
               $"3. Return ONLY the direct translation.";
    }

    private static string BuildBatchSystemPrompt(string targetLanguage)
    {
        return $"You are a professional technical translator specializing in software engineering and computer science. " +
               $"Translate the provided list of texts into {targetLanguage}. " +
               $"Return a JSON object with a single property 'translations' containing an array of translated strings matching the exact size and order of the input array. " +
               $"Keep code terms, class names, and annotations in their original English form.";
    }

    private static string BuildDictionarySystemPrompt(string targetLanguage)
    {
        return $"You are a professional technical translator specializing in software engineering and computer science. " +
               $"Translate the text values of the provided JSON object into {targetLanguage}. " +
               $"IMPORTANT RULES: " +
               $"1. Return a JSON object with the exact same keys as the input, where each value is the translated text. " +
               $"2. Keep code terms, class names, method names, variable names, annotations, and identifiers (e.g. Facade, InventoryService, @Service, OrderFacade, Interface, Class) in their original English/code form. " +
               $"3. Do NOT omit any keys from the input. " +
               $"4. Return ONLY valid JSON matching the input keys.";
    }

    private static string CleanJsonString(string raw)
    {
        var clean = raw.Trim();
        if (clean.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(7);
        }
        else if (clean.StartsWith("```"))
        {
            clean = clean.Substring(3);
        }

        if (clean.EndsWith("```"))
        {
            clean = clean.Substring(0, clean.Length - 3);
        }

        return clean.Trim();
    }
}

