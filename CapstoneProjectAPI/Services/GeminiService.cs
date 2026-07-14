using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CapstoneProjectAPI.Interfaces;

namespace CapstoneProjectAPI.Services
{
    /// <summary>
    /// Calls the Gemini REST API (Google AI Studio) to generate a concise AI summary
    /// of an uploaded document. Uses inline base64 encoding so no separate File API
    /// upload step is needed for files ≤ 5 MB.
    /// </summary>
    public class GeminiService : IGeminiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiService> _logger;

        // Maximum inline payload size accepted by the Gemini inline-data API (20 MB raw → ~27 MB base64).
        // Our upload limit is already 5 MB so this is always safe.
        private const int MaxInlineSizeBytes = 20 * 1024 * 1024;

        public GeminiService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<GeminiService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<string?> GenerateSummaryAsync(Stream fileStream, string mimeType, string fileName)
        {
            var apiKey = _configuration["GeminiApi:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("GeminiApi:ApiKey is not configured. Skipping AI summary for '{FileName}'.", fileName);
                return null;
            }

            var model = _configuration["GeminiApi:Model"] ?? "gemini-2.0-flash";

            // Read file bytes (stream must be readable; callers open a fresh stream for us)
            byte[] fileBytes;
            try
            {
                using var ms = new MemoryStream();
                await fileStream.CopyToAsync(ms);
                fileBytes = ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read file stream for AI summary ('{FileName}').", fileName);
                return null;
            }

            if (fileBytes.Length > MaxInlineSizeBytes)
            {
                _logger.LogWarning(
                    "File '{FileName}' ({Size} bytes) exceeds the inline payload limit. Skipping AI summary.",
                    fileName, fileBytes.Length);
                return null;
            }

            string base64Data = Convert.ToBase64String(fileBytes);
            string prompt = BuildPrompt(mimeType);

            // Build the Gemini generateContent request body
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Data
                                }
                            },
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 360
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            try
            {
                var client = _httpClientFactory.CreateClient("GeminiClient");
                client.Timeout = TimeSpan.FromSeconds(30);

                using var response = await client.PostAsJsonAsync(url, requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Gemini API returned {StatusCode} for '{FileName}'. Body: {Body}",
                        response.StatusCode, fileName, errorBody);
                    return null;
                }

                var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
                var summaryText = ExtractText(responseJson);

                if (string.IsNullOrWhiteSpace(summaryText))
                {
                    _logger.LogWarning("Gemini returned an empty summary for '{FileName}'.", fileName);
                    return null;
                }

                _logger.LogInformation("AI summary generated successfully for '{FileName}'.", fileName);
                return summaryText.Trim();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken == default)
            {
                _logger.LogWarning(ex, "Gemini API request timed out for '{FileName}'.", fileName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error calling Gemini API for '{FileName}'.", fileName);
                return null;
            }
        }

        /// <summary>
        /// Selects a tailored prompt based on the MIME type of the document.
        /// PDFs are treated as textual documents; JPEG/PNG are treated as images.
        /// </summary>
        private static string BuildPrompt(string mimeType)
        {
            if (mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return
                    "You are a document management assistant. " +
                    "Read the full content of this PDF document and produce a concise summary of 2 to 4 sentences. " +
                    "Focus on: the document's main topic or subject, its primary purpose or intent, " +
                    "and any key facts, figures, or conclusions it contains. " +
                    "Write the summary in plain professional English without bullet points or headings.";
            }

            // image/jpeg or image/png
            return
                "You are a document management assistant. " +
                "Analyse the image carefully and produce a concise description of 2 to 4 sentences. " +
                "Focus on: what type of document or image this appears to be (e.g. invoice, chart, photo, certificate), " +
                "the main subject or content visible, and any notable text, data, or information shown. " +
                "Write the description in plain professional English without bullet points or headings.";
        }

        /// <summary>
        /// Navigates the Gemini response JSON to extract the generated text.
        /// Path: candidates[0].content.parts[0].text
        /// </summary>
        private static string? ExtractText(JsonElement root)
        {
            try
            {
                return root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();
            }
            catch (Exception)
            {
                // If the JSON shape is unexpected (e.g. safety block), return null
                return null;
            }
        }
    }
}
