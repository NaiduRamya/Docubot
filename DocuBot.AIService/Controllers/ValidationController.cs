using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using DocuBot.AIService.Models;
using DocuBot.AIService.Providers;

namespace DocuBot.AIService.Controllers
{
    [ApiController]
    [Route("ai")]
    public class ValidationController : ControllerBase
    {
        private readonly IAiProvider _aiProvider;

        public ValidationController(IAiProvider aiProvider)
        {
            _aiProvider = aiProvider;
        }

        [HttpPost("validate-commit")]
        public async Task<IActionResult> ValidateCommit([FromBody] SimpleCommitRequest req)
        {
            return await ExecuteAgentFlow($"I have a commit message: '{req.Message}'. Please analyze if it is perfectly conventional.");
        }

        [HttpPost("validate-branch")]
        public async Task<IActionResult> ValidateBranch([FromBody] BranchRequest req)
        {
            return await ExecuteAgentFlow($"I have a branch name: '{req.Branch}'. Please analyze if it follows standard branch formatting.");
        }

        private async Task<IActionResult> ExecuteAgentFlow(string userPrompt)
        {
            var systemPrompt = "You are DocuBot, a strict git validation assistant. Analyze the input and provide a validation result. Your final output should be ONLY a raw JSON strictly matching: {\"isValid\": true/false, \"reason\": \"string\"}";
            var result = await _aiProvider.ValidateAsync(systemPrompt, userPrompt);
            return Ok(result);
        }
    }
}
