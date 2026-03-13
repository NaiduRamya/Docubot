using System.Text.Json;
using DocuBot.Cli.Models;

namespace DocuBot.Cli.AI;

public static class AiParsingUtils
{
    public static ValidationResult ParseValidationResult(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return new ValidationResult { IsValid = false, Reason = "Empty response from AI" };

        try
        {
            var cleanJson = rawInput.Trim().Trim('`');
            if (cleanJson.StartsWith("json")) cleanJson = cleanJson.Substring(4).Trim();

            var validationResult = JsonSerializer.Deserialize<ValidationResult>(cleanJson, new JsonSerializerOptions 
            {
                PropertyNameCaseInsensitive = true 
            });

            return validationResult ?? new ValidationResult { IsValid = false, Reason = "Agent responded with null result format" };
        }
        catch
        {
            return new ValidationResult { IsValid = false, Reason = "Agent responded with non-JSON format: " + rawInput };
        }
    }
}
