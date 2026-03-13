namespace DocuBot.AIService.Models;

public class GeminiRequest
{
    public List<GeminiContent> contents { get; set; } = new();
    public GeminiSystemInstruction? systemInstruction { get; set; }
}

public class GeminiSystemInstruction
{
    public GeminiContentParts parts { get; set; } = new();
}

public class GeminiContent
{
    public string role { get; set; } = string.Empty;
    public List<GeminiContentParts> parts { get; set; } = new();
}

public class GeminiContentParts
{
    public string text { get; set; } = string.Empty;
}

public class GeminiResponse
{
    public List<GeminiCandidate> candidates { get; set; } = new();
}

public class GeminiCandidate
{
    public GeminiContent content { get; set; } = new();
}
