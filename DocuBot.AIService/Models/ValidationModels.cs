using System.Text.Json;

namespace DocuBot.AIService.Models
{
    public record BranchRequest(string Branch);
    public record SimpleCommitRequest(string Message);

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string? Reason { get; set; }
    }

    public class McpResponse
    {
        public string Jsonrpc { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public JsonElement? Result { get; set; }
    }

    // Required for Ollama robust chat API parsing
    public class OllamaChatResponse
    {
        public string model { get; set; } = string.Empty;
        public ChatMessage message { get; set; } = new();
    }

    public class ChatMessage
    {
        public string role { get; set; } = string.Empty;
        public string content { get; set; } = string.Empty;
        public List<ToolCall>? tool_calls { get; set; }
    }

    public class ToolCall
    {
        public ToolFunction function { get; set; } = new();
    }

    public class ToolFunction
    {
        public string name { get; set; } = string.Empty;
        public JsonElement arguments { get; set; }
    }
}
