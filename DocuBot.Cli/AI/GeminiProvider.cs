using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Configuration;
using DocuBot.Cli.Models;

namespace DocuBot.Cli.AI;

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
        
        var rawJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Gemini API error ({response.StatusCode}): {rawJson}");
        }

        var result = JsonSerializer.Deserialize<GeminiResponse>(rawJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        var text = result?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text;
        
        if (string.IsNullOrWhiteSpace(text))
        {
            // If we have candidates but no text, maybe it was blocked
            var candidate = result?.candidates?.FirstOrDefault();
            if (candidate != null && (candidate.content == null || candidate.content.parts == null || !candidate.content.parts.Any()))
            {
                throw new Exception($"Gemini returned a candidate with no content. This usually means the response was blocked by safety filters. Raw response: {rawJson}");
            }

            if (result?.candidates == null || !result.candidates.Any())
            {
                 throw new Exception($"Gemini returned no candidates. Raw response: {rawJson}");
            }
        }

        return text ?? string.Empty;
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
