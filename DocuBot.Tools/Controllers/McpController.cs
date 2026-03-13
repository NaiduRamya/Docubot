using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocuBot.Tools.Models;

namespace DocuBot.Tools.Controllers
{
    [ApiController]
    [Route("mcp")]
    public class McpController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> HandleJsonRpc([FromBody] JsonRpcRequest request)
        {
            if (request.Jsonrpc != "2.0")
            {
                return BadRequest("Only JSON-RPC 2.0 is supported");
            }

            try
            {
                switch (request.Method)
                {
                    case "initialize":
                        return Ok(HandleInitialize(request.Id));

                    case "tools/list":
                        return Ok(HandleToolsList(request.Id));

                    case "tools/call":
                        var result = await HandleToolsCall(request.Params);
                        return Ok(new JsonRpcResponse 
                        { 
                            Jsonrpc = "2.0", 
                            Id = request.Id ?? "", 
                            Result = result 
                        });

                    default:
                        return Ok(new JsonRpcErrorResponse
                        {
                            Jsonrpc = "2.0",
                            Id = request.Id ?? "",
                            Error = new JsonRpcError { Code = -32601, Message = "Method not found" }
                        });
                }
            }
            catch (Exception ex)
            {
                return Ok(new JsonRpcErrorResponse
                {
                    Jsonrpc = "2.0",
                    Id = request.Id ?? "",
                    Error = new JsonRpcError { Code = -32000, Message = ex.Message }
                });
            }
        }

        private JsonRpcResponse HandleInitialize(string? id)
        {
            return new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Id = id ?? "",
                Result = new
                {
                    protocolVersion = "2024-11-05", // Standard MCP version
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = "DocuBot.Tools", version = "1.0.0" }
                }
            };
        }

        private JsonRpcResponse HandleToolsList(string? id)
        {
            return new JsonRpcResponse
            {
                Jsonrpc = "2.0",
                Id = id ?? "",
                Result = new
                {
                    tools = new[]
                    {
                        new
                        {
                            name = "git_diff",
                            description = "Gets the current git diff of the repository.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new Dictionary<string, object>(), // no properties, properly typed
                                required = Array.Empty<string>()
                            }
                        },
                        new
                        {
                            name = "validate_branch",
                            description = "Validates if a branch name follows standard naming conventions.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new Dictionary<string, object>
                                {
                                    { "branchName", new { type = "string", description = "The name of the branch to validate" } }
                                },
                                required = new[] { "branchName" }
                            }
                        },
                        new
                        {
                            name = "analyze_commit",
                            description = "Analyzes a commit message for conventional commit format compliance.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new Dictionary<string, object>
                                {
                                    { "message", new { type = "string", description = "The commit message to analyze" } }
                                },
                                required = new[] { "message" }
                            }
                        },
                        new
                        {
                            name = "generate_docs",
                            description = "Generates a documentation stub for a given piece of code.",
                            inputSchema = new
                            {
                                type = "object",
                                properties = new Dictionary<string, object>
                                {
                                    { "code", new { type = "string", description = "The source code to document" } }
                                },
                                required = new[] { "code" }
                            }
                        }
                    }
                }
            };
        }

        private async Task<object> HandleToolsCall(JsonElement? parameters)
        {
            if (parameters == null)
                throw new Exception("Missing parameters for tools/call");

            var name = parameters.Value.GetProperty("name").GetString();
            var args = parameters.Value.TryGetProperty("arguments", out var a) ? a : default;

            var contentResult = "";

            switch (name)
            {
                case "git_diff":
                    contentResult = await ExecuteGitDiff();
                    break;

                case "validate_branch":
                    var branchName = args.GetProperty("branchName").GetString() ?? "";
                    contentResult = ExecuteValidateBranch(branchName);
                    break;

                case "analyze_commit":
                    var message = args.GetProperty("message").GetString() ?? "";
                    contentResult = ExecuteAnalyzeCommit(message);
                    break;

                case "generate_docs":
                    var code = args.GetProperty("code").GetString() ?? "";
                    contentResult = $"/// <summary>\n/// Automatically generated documentation stub.\n/// </summary>\n// Original Code Length: {code.Length}";
                    break;

                default:
                    throw new Exception($"Tool {name} not found");
            }

            return new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = contentResult
                    }
                }
            };
        }

        private async Task<string> ExecuteGitDiff()
        {
            var process = new Process();
            process.StartInfo.FileName = "git";
            process.StartInfo.Arguments = "diff";
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrWhiteSpace(output) ? "No changes" : output;
        }

        private string ExecuteValidateBranch(string branchName)
        {
            if (string.IsNullOrWhiteSpace(branchName))
                return "{\"valid\": false, \"reason\": \"Branch name empty\"}";

            bool isValid = Regex.IsMatch(branchName, "^(feature|bugfix|hotfix|release)/[a-zA-Z0-9_-]+$")
                           || branchName == "main" || branchName == "master";

            return JsonSerializer.Serialize(new { valid = isValid, pattern = "^(feature|bugfix|hotfix|release)/[a-zA-Z0-9_-]+$" });
        }

        private string ExecuteAnalyzeCommit(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "{\"isConventional\": false, \"reason\": \"Commit message empty\"}";

            bool isConv = Regex.IsMatch(message, "^(feat|fix|docs|style|refactor|perf|test|build|ci|chore|revert)(\\([a-zA-Z0-9_-]+\\))?: .+$");

            return JsonSerializer.Serialize(new
            {
                isConventional = isConv,
                messageLength = message.Length,
                hasTicketRef = Regex.IsMatch(message, "#[0-9]+")
            });
        }
    }
}
