using DocuBot.Cli.Models;

namespace DocuBot.Cli.AI;

public interface IAiProvider
{
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt);
    Task<ValidationResult> ValidateAsync(string systemPrompt, string userPrompt);
}
