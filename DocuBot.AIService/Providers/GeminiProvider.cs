using System.Net.Http.Json;
using System.Text.Json;
using DocuBot.AIService.Models;

namespace DocuBot.AIService.Providers;

public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _geminiGenerateUrl;
    private readonly string _apiKey;

    public GeminiProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _geminiGenerateUrl = config["AiSettings:GeminiGenerateUrl"] ?? "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
        _apiKey = config["AiSettings:GeminiApiKey"] ?? string.Empty;
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt)
    {
        var request = BuildGeminiRequest(systemPrompt, userPrompt);
        
        var url = $"{_geminiGenerateUrl}?key={_apiKey}";
        var response = await _http.PostAsJsonAsync(url, request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Gemini API error ({response.StatusCode}): {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        
        var text = result?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text ?? string.Empty;
        return text;
    }

    public async Task<ValidationResult> ValidateAsync(string systemPrompt, string userPrompt)
    {
        try
        {
            var responseText = await GenerateTextAsync(systemPrompt, userPrompt);
            return AiParsingUtils.ParseValidationResult(responseText);
        }
        catch (Exception ex)
        {
            return new ValidationResult { IsValid = false, Reason = $"Agent failed to execute: {ex.Message}" };
        }
    }

    private GeminiRequest BuildGeminiRequest(string systemPrompt, string userPrompt)
    {
        var request = new GeminiRequest
        {
            contents = new List<GeminiContent>
            {
                new GeminiContent
                {
                    role = "user",
                    parts = new List<GeminiContentParts> { new GeminiContentParts { text = userPrompt } }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            request.systemInstruction = new GeminiSystemInstruction
            {
                parts = new GeminiContentParts { text = systemPrompt }
            };
        }

        return request;
    }
}
