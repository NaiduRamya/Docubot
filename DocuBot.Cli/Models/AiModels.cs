namespace DocuBot.Cli.Models;

public record CommitRequest(string Message, string Diff);

public class OllamaResponse
{
    public string prompt { get; set; } = string.Empty;
    public string response { get; set; } = string.Empty;
}

public class GenerateDocsRequest
{
    public string Diff { get; set; } = string.Empty;
}

public class GenerateDocsAIResponse
{
    public string FileName { get; set; } = string.Empty;
    public string Documentation { get; set; } = string.Empty;
}
