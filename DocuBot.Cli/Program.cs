using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DocuBot.Cli.Models;
using DocuBot.Cli.AI;

static class Program
{
    private static IAiProvider _aiProvider = null!;
    private static IConfiguration _config = null!;
    private static readonly HttpClient _httpClient = new HttpClient();

    static async Task Main(string[] args)
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        InitializeAiProvider();

        if (args.Length == 0)
        {
            Console.WriteLine("DocuBot CLI");
            return;
        }

        var command = args[0];

        switch (command)
        {
            case "init":
                Init();
                break;

            case "validate-commit":
                if (args.Length < 2)
                {
                    Console.WriteLine("Missing file path for commit message.");
                    Environment.Exit(1);
                }
                bool validateAutoApply = args.Length > 2 && args[2] == "--auto";
                await ValidateCommit(args[1], validateAutoApply);
                break;

            case "validate-branch":
                await ValidateBranch();
                break;

            case "suggest-commit":
                bool autoApply = args.Length > 2 && args[2] == "--auto";
                await SuggestCommit(args.Length > 1 && args[1] != "--auto" ? args[1] : null, autoApply);
                break;

            case "generate-docs":
                await GenerateDocs();
                break;

            default:
                Console.WriteLine($"Unknown command: {command}");
                break;
        }
    }

    private static void InitializeAiProvider()
    {
        var providerType = _config["AiSettings:Provider"] ?? "Ollama";
        if (providerType.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
        {
            _aiProvider = new GeminiProvider(_httpClient, _config);
        }
        else
        {
            _aiProvider = new OllamaProvider(_httpClient, _config);
        }
    }

    static void Init()
    {
        Console.WriteLine("Initializing DocuBot Husky hooks...");
        
        RunProcess("dotnet", "new tool-manifest --force");
        RunProcess("dotnet", "tool install Husky");
        RunProcess("dotnet", "husky install");
        
        // Ensure hooks are created with specific commands
        EnsureHookCommand("commit-msg", "docubot validate-commit ${1}");
        EnsureHookCommand("prepare-commit-msg", "docubot suggest-commit ${1}");
        EnsureHookCommand("pre-commit", "docubot validate-branch");
        EnsureHookCommand("post-commit", "docubot generate-docs");

        Console.WriteLine("Husky hooks installed and configured.");
    }

    static void EnsureHookCommand(string hookName, string command)
    {
        string hookPath = Path.Combine(".husky", hookName);
        if (File.Exists(hookPath))
        {
            string content = File.ReadAllText(hookPath);
            // Check if exact command exists, or if a variant with --auto exists
            if (content.Contains(command))
            {
                return;
            }

            // Cleanup old variants if they exist to keep it clean
            if (content.Contains(command + " --auto"))
            {
                Console.WriteLine($"Updating old hook command in {hookName}...");
                File.WriteAllText(hookPath, content.Replace(command + " --auto", command));
                return;
            }
        }
        
        RunProcess("dotnet", $"husky add {hookName} -c \"{command}\"");
    }

    static async Task ValidateCommit(string filePath, bool autoApply = false)
    {
        Console.WriteLine("Validating commit message via AI Provider...");
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine("Commit file not found.");
            Environment.Exit(1);
        }

        var message = await File.ReadAllTextAsync(filePath);
        
        // Let's filter out lines starting with '#' (git comments)
        var messageLines = message.Split('\n').Where(l => !l.TrimStart().StartsWith("#"));
        message = string.Join('\n', messageLines).Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            // Empty message means the user just clicked "Commit" without typing anything.
            // In this case, we just trigger the suggestion instantly without trying to "validate" an empty string.
            bool applied = await SuggestCommit(filePath, false);
            Environment.Exit(applied ? 0 : 1);
        }

        try
        {
            var systemPrompt = "You are DocuBot, a strict git validation assistant. Analyze the input and provide a validation result. Your final output should be ONLY a raw JSON strictly matching: {\"isValid\": true/false, \"reason\": \"string\"}";
            var userPrompt = $"I have a commit message: '{message}'. Please analyze if it is perfectly conventional.";
            
            var result = await _aiProvider.ValidateAsync(systemPrompt, userPrompt);
            
            if (result != null && result.IsValid)
            {
                Console.WriteLine("Commit message valid.");
                return;
            }
            Console.WriteLine($"Commit validation failed: {result?.Reason}");
            Console.WriteLine();
            bool applied = await SuggestCommit(filePath, false);
            Environment.Exit(applied ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during commit validation: {ex.Message}");
            // Important: Do not block the developer from committing if the AI fails.
            Environment.Exit(0);
        }
    }

    static async Task<bool> SuggestCommit(string? filePath, bool autoApply = false)
    {
        string currentMessage = "";
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            currentMessage = await File.ReadAllTextAsync(filePath);
        }

        string diff = GetStagedDiff();
        if (string.IsNullOrWhiteSpace(diff))
        {
            Console.WriteLine("No staged changes found. Please stage your changes with 'git add' to get a better suggestion.");
        }

        Console.WriteLine("Fetching commit suggestion from AI Provider...");

        var prompt = $"""
                        Improve the commit message using conventional commits.

                        Commit message:
                        {currentMessage}

                        Code changes:
                        {diff}

                        Return only improved commit message.
                      """;

        try
        {
            var suggestion = await _aiProvider.GenerateTextAsync(string.Empty, prompt);

            if (!string.IsNullOrWhiteSpace(suggestion))
            {
                Console.WriteLine("--------------------------------------------------");
                Console.WriteLine("Suggested Commit Message:");
                Console.WriteLine(suggestion);
                Console.WriteLine("--------------------------------------------------");
                
                if (autoApply)
                {
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await File.WriteAllTextAsync(filePath, suggestion);
                        Console.WriteLine("Auto-applied suggestion to commit message.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("No commit message file specified for auto-apply.");
                        return false;
                    }
                }

                Console.Write("Apply this suggestion? (y/n): ");
                var input = ReadLineFromConsole()?.Trim().ToLower();
                
                if (input == "y" || input == "yes")
                {
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        await File.WriteAllTextAsync(filePath, suggestion);
                        Console.WriteLine("Suggestion applied to commit message.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("No commit message file specified (use as hook to apply automatically).");
                        Console.WriteLine("Copy the message above to use it manually.");
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("Suggestion declined.");
                    return false;
                }
            }
            Console.WriteLine("AI Provider did not provide a suggestion.");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating commit suggestion: {ex.Message}");
            return false;
        }
    }

    static async Task GenerateDocs()
    {
        Console.WriteLine("Generating technical documentation from recent commit...");

        string diff = GetLastCommitDiff();
        if (string.IsNullOrWhiteSpace(diff))
        {
            Console.WriteLine("No changes found in the last commit to document.");
            return;
        }

        var prompt = $"""
                        You are an expert technical writer. Write a clear, concise technical documentation summary in Markdown format based on following code changes.
                        Focus on what changed and why it matters.

                        Code changes:
                        {diff}

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
        
            GenerateDocsAIResponse? parsed = null;
            try 
            {
                parsed = JsonSerializer.Deserialize<GenerateDocsAIResponse>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* Skip parsing if it fails */ }

            var documentation = parsed?.Documentation ?? docsRaw;
            var fileName = parsed?.FileName ?? "TECHNICAL_DOCS.md";
            
            var invalids = Path.GetInvalidFileNameChars();
            fileName = string.Join("_", fileName.Split(invalids, StringSplitOptions.RemoveEmptyEntries));
            if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".md";
            }

            if (!Directory.Exists("docs")) Directory.CreateDirectory("docs");
            var filePath = Path.Combine("docs", fileName);

            await File.WriteAllTextAsync(filePath, documentation);
            Console.WriteLine($"Successfully generated documentation at {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating documentation: {ex.Message}");
        }
    }

    static string GetStagedDiff()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff --cached",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        return process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
    }

    static async Task ValidateBranch()
    {
        string branchName = GetCurrentBranch();
        if (string.IsNullOrEmpty(branchName))
        {
            Console.WriteLine("Could not determine current Git branch.");
            Environment.Exit(1);
        }

        Console.WriteLine($"Validating branch name '{branchName}' via AI Provider...");
        
        try
        {
            var systemPrompt = "You are DocuBot, a strict git validation assistant. Analyze the input and provide a validation result. Your final output should be ONLY a raw JSON strictly matching: {\"isValid\": true/false, \"reason\": \"string\"}";
            var userPrompt = $"I have a branch name: '{branchName}'. Please analyze if it follows standard branch formatting.";
            
            var result = await _aiProvider.ValidateAsync(systemPrompt, userPrompt);
            
            if (result != null && result.IsValid)
            {
                Console.WriteLine("Branch name valid.");
                return;
            }
            Console.WriteLine($"Branch validation failed: {result?.Reason}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during branch validation: {ex.Message}");
            Environment.Exit(0);
        }
    }

    static string GetCurrentBranch()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --abbrev-ref HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        return process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
    }

    static string GetLastCommitDiff()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "diff HEAD~1 HEAD",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
        return process?.StandardOutput.ReadToEnd().Trim() ?? string.Empty;
    }

    static string? ReadLineFromConsole()
    {
        if (!Console.IsInputRedirected)
        {
            return Console.ReadLine();
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var stream = File.Open("CONIN$", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadLine();
            }
            else
            {
                using var stream = File.Open("/dev/tty", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadLine();
            }
        }
        catch
        {
            return null;
        }
    }

    static void RunProcess(string fileName, string arguments)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = true
        });
        
        process?.WaitForExit();
    }
}