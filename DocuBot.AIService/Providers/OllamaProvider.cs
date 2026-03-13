using System.Net.Http.Json;
using System.Text.Json;
using DocuBot.AIService.Models;

namespace DocuBot.AIService.Providers;

public class OllamaProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _ollamaChatUrl;
    private readonly string _ollamaGenerateUrl;

    public OllamaProvider(HttpClient http, IConfiguration config)
    {
        _http = http;
        _ollamaChatUrl = config["AiSettings:OllamaChatUrl"] ?? string.Empty;
        _ollamaGenerateUrl = config["AiSettings:OllamaGenerateUrl"] ?? string.Empty;
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt)
    {
        var prompt = string.IsNullOrWhiteSpace(systemPrompt) 
            ? userPrompt 
            : $"{systemPrompt}\n\n{userPrompt}";

        var body = new
        {
            model = "phi3",
            prompt = prompt,
            stream = false
        };

        var response = await _http.PostAsJsonAsync(_ollamaGenerateUrl, body);
        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();

        return result?.response ?? string.Empty;
    }

    public async Task<ValidationResult> ValidateAsync(string systemPrompt, string userPrompt)
    {
        var messages = new List<object>();
        
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }
        
        messages.Add(new { role = "user", content = userPrompt });

        var ollamaReq = new
        {
            model = "llama3.1",
            messages = messages,
            stream = false
        };

        try
        {
            var response = await _http.PostAsJsonAsync(_ollamaChatUrl, ollamaReq);
            var chatResult = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            
            return AiParsingUtils.ParseValidationResult(chatResult?.message?.content);
        }
        catch (Exception ex)
        {
            return new ValidationResult { IsValid = false, Reason = $"Agent failed to execute: {ex.Message}" };
        }
    }
}
