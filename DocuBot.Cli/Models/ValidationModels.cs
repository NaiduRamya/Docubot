using System.Text.Json;

namespace DocuBot.Cli.Models;

public record BranchRequest(string Branch);
public record SimpleCommitRequest(string Message);

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? Reason { get; set; }
}

public class ValidationResponse : ValidationResult { }
public class SuggestionResponse { public string? Suggestion { get; set; } }
public class GenerateDocsResponse 
{ 
    public string? Documentation { get; set; } 
    public string? FileName { get; set; } 
}

public class OllamaChatResponse
{
    public string model { get; set; } = string.Empty;
    public ChatMessage message { get; set; } = new();
}

public class ChatMessage
{
    public string role { get; set; } = string.Empty;
    public string content { get; set; } = string.Empty;
}
