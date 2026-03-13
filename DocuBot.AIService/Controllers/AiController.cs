using Microsoft.AspNetCore.Mvc;
using DocuBot.AIService.Models;
using DocuBot.AIService.Providers;

namespace DocuBot.AIService.Controllers
{
    [ApiController]
    [Route("ai")]
    public class AiController : ControllerBase
    {
        private readonly IAiProvider _aiProvider;

        public AiController(IAiProvider aiProvider)
        {
            _aiProvider = aiProvider;
        }
        [HttpPost("commit-suggestion")]
        public async Task<IActionResult> SuggestCommit([FromBody] CommitRequest req)
        {
            var prompt = $"""
                            Improve the commit message using conventional commits.

                            Commit message:
                            {req.Message}

                            Code changes:
                            {req.Diff}

                            Return only improved commit message.
                          """;

            try
            {
                var suggestion = await _aiProvider.GenerateTextAsync(string.Empty, prompt);
                return Ok(new { suggestion = suggestion });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("generate-docs")]
        public async Task<IActionResult> GenerateDocs([FromBody] GenerateDocsRequest req)
        {
            var prompt = $"""
                            You are an expert technical writer. Write a clear, concise technical documentation summary in Markdown format based on following code changes.
                            Focus on what changed and why it matters.

                            Code changes:
                            {req.Diff}

                            Return a JSON object with two properties:
                            1. "fileName": A suitable markdown file name for these changes (e.g. "authentication-setup.md").
                            2. "documentation": The technical documentation in Markdown format.

                            Return ONLY the raw JSON, do not wrap in markdown blocks.
                          """;

            try
            {
                var docsRaw = await _aiProvider.GenerateTextAsync(string.Empty, prompt);

                var cleanJson = docsRaw.Trim().Trim('`');
                if (cleanJson.StartsWith("json")) cleanJson = cleanJson.Substring(4).Trim();
            
                try 
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<GenerateDocsAIResponse>(cleanJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return Ok(new { documentation = parsed?.Documentation ?? docsRaw, fileName = parsed?.FileName ?? "TECHNICAL_DOCS.md" });
                }
                catch
                {
                    return Ok(new { documentation = docsRaw, fileName = "TECHNICAL_DOCS.md" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
