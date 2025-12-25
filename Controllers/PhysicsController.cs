using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using ExamCreateApp.Services;
using ExamCreateApp.Models;
using ExamCreateApp.Data;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PhysicsController : ControllerBase
{
    private readonly GeminiService _geminiService;
    private readonly RAGService _ragService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PhysicsController> _logger;

    public PhysicsController(
        GeminiService geminiService,
        RAGService ragService,
        AppDbContext dbContext,
        ILogger<PhysicsController> logger)
    {
        _geminiService = geminiService;
        _ragService = ragService;
        _dbContext = dbContext;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    [HttpPost("generate-tasks")]
    public async Task<ActionResult<TaskGenerationResponse>> GenerateTasks(
        [FromBody] TaskGenerationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation($"🔵 User {userId} generating tasks");

            var searchQuery = $"{request.PhysicsSubject} {request.TaskTopic}";

            var similarTasks = await _ragService.SearchSimilarTasks(
                searchQuery,
                limit: 5,
                level: request.DifficultyLevel,
                subject: request.PhysicsSubject
            );

            var context = _ragService.BuildContextFromExamSheets(similarTasks);
            var result = await _geminiService.GenerateExamTasks(request, context);

            var cleanJson = CleanJson(result);
            _logger.LogInformation($"📋 Cleaned JSON: {cleanJson.Substring(0, Math.Min(500, cleanJson.Length))}...");

            var response = JsonSerializer.Deserialize<TaskGenerationResponse>(
                cleanJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                }
            );

            if (response == null || response.Tasks == null || response.Tasks.Count == 0)
            {
                return BadRequest(new
                {
                    error = "Failed to generate tasks",
                    details = "No tasks in response",
                    rawResponse = cleanJson
                });
            }

            // ✅ Zapisz do GeneratedTasks
            try
            {
                var generatedTask = new GeneratedTask
                {
                    UserId = userId,
                    Prompt = $"{request.TaskTopic} - {request.DifficultyLevel} - {request.PhysicsSubject}",
                    GeneratedText = cleanJson,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.GeneratedTasks.AddAsync(generatedTask);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"💾 Saved to GeneratedTasks (ID: {generatedTask.Id})");
            }
            catch (Exception saveEx)
            {
                _logger.LogWarning(saveEx, "⚠️ Failed to save to GeneratedTasks, but continuing...");
            }

            return Ok(response);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "❌ JSON parsing error");
            return StatusCode(500, new
            {
                error = "Failed to parse response",
                details = ex.Message,
                position = $"Line {ex.LineNumber}, Pos {ex.BytePositionInLine}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating tasks");
            return StatusCode(500, new
            {
                error = "Failed to generate tasks",
                details = ex.Message
            });
        }
    }

    // ✅ METODY POMOCNICZE - CleanJson i FixIncompleteJson
    private string CleanJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "{\"tasks\":[]}";

        json = json.Trim();

        if (json.StartsWith("```json"))
            json = json.Substring("```json".Length);
        else if (json.StartsWith("```"))
            json = json.Substring("```".Length);

        if (json.EndsWith("```"))
            json = json.Substring(0, json.Length - 3);

        json = json.Trim();

        var firstBrace = json.IndexOf('{');
        var lastBrace = json.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            json = json.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        json = FixIncompleteJson(json);

        return json;
    }

    private string FixIncompleteJson(string json)
    {
        try
        {
            JsonDocument.Parse(json);
            return json;
        }
        catch
        {
            _logger.LogWarning("⚠️ JSON is malformed, attempting to fix...");

            int openBraces = json.Count(c => c == '{');
            int closeBraces = json.Count(c => c == '}');
            int openBrackets = json.Count(c => c == '[');
            int closeBrackets = json.Count(c => c == ']');

            while (closeBraces < openBraces)
            {
                json += "\n}";
                closeBraces++;
                _logger.LogInformation("➕ Added missing }");
            }

            while (closeBrackets < openBrackets)
            {
                json += "\n]";
                closeBrackets++;
                _logger.LogInformation("➕ Added missing ]");
            }

            try
            {
                JsonDocument.Parse(json);
                _logger.LogInformation("✅ JSON fixed successfully!");
                return json;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Could not fix JSON: {ex.Message}");
                return "{\"tasks\":[]}";
            }
        }
    }

    [HttpGet("statistics")]
    public async Task<ActionResult> GetStatistics()
    {
        try
        {
            var stats = await _ragService.GetStatistics();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<ExamTask>>> SearchTasks(
        [FromQuery] string query,
        [FromQuery] string? level = null,
        [FromQuery] string? subject = null,
        [FromQuery] int limit = 10)
    {
        try
        {
            var results = await _ragService.SearchSimilarTasks(query, limit, level, subject);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error searching tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public ActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "OK",
            message = "Physics API is running",
            timestamp = DateTime.UtcNow
        });
    }
}