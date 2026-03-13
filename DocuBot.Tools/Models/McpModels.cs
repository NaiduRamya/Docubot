using System.Text.Json;

namespace DocuBot.Tools.Models
{
    public class JsonRpcRequest
    {
        public string Jsonrpc { get; set; } = "2.0";
        public string? Id { get; set; } = Guid.NewGuid().ToString();
        public string Method { get; set; } = string.Empty;
        public JsonElement? Params { get; set; }
    }

    public class JsonRpcResponse
    {
        public string Jsonrpc { get; set; } = "2.0";
        public string Id { get; set; } = string.Empty;
        public object? Result { get; set; }
    }

    public class JsonRpcErrorResponse
    {
        public string Jsonrpc { get; set; } = "2.0";
        public string Id { get; set; } = string.Empty;
        public JsonRpcError Error { get; set; } = new JsonRpcError();
    }

    public class JsonRpcError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
