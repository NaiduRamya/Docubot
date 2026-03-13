namespace DocuBot.AIService.Providers;

using DocuBot.AIService.Models;

public interface IAiProvider
{
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt);
    Task<ValidationResult> ValidateAsync(string systemPrompt, string userPrompt);
}
