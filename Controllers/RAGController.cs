using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExamCreateApp.Services;
using ExamCreateApp.Models;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RAGController : ControllerBase
{
    private readonly RAGService _ragService;
    private readonly ILogger<RAGController> _logger;

    public RAGController(RAGService ragService, ILogger<RAGController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    // GET: api/rag/statistics - Już MASZ to w PhysicsController, ale dodajmy tutaj też
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

    // ✅ NOWE - Test kontekstu RAG
    [HttpGet("test-context")]
    public async Task<ActionResult> TestContext(
        [FromQuery] string topic = "predkosc",
        [FromQuery] string level = "podstawowy",
        [FromQuery] string subject = "mechanika")
    {
        try
        {
            _logger.LogInformation($"🔍 Testing RAG context: topic={topic}, level={level}, subject={subject}");

            var tasks = await _ragService.SearchSimilarTasks(
                topic,
                limit: 5,
                level: level,
                subject: subject
            );

            var context = _ragService.BuildContextFromExamSheets(tasks);

            _logger.LogInformation($"📊 Found {tasks.Count} tasks");

            return Ok(new
            {
                tasksFound = tasks.Count,
                context = context,
                tasks = tasks.Select(t => new
                {
                    t.Id,
                    t.TaskNumber,
                    t.ExamSheetName,
                    t.Year,
                    t.Level,
                    t.Subject,
                    ContentPreview = t.Content.Length > 150
                        ? t.Content.Substring(0, 150) + "..."
                        : t.Content
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error testing RAG context");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ NOWE - Sprawdź czy RAG ma dane
    [HttpGet("health")]
    public async Task<ActionResult> HealthCheck()
    {
        try
        {
            var stats = await _ragService.GetStatistics();

            // Parse stats (it's anonymous object)
            var statsJson = System.Text.Json.JsonSerializer.Serialize(stats);
            var statsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(statsJson);

            var totalTasks = 0;
            if (statsDict != null && statsDict.TryGetValue("TotalTasks", out var total))
            {
                totalTasks = Convert.ToInt32(total);
            }

            var isHealthy = totalTasks > 0;

            return Ok(new
            {
                status = isHealthy ? "healthy" : "unhealthy",
                message = isHealthy
                    ? $"RAG is working! Database has {totalTasks} tasks."
                    : "RAG database is empty! Please import exam sheets.",
                totalTasks = totalTasks,
                recommendation = isHealthy
                    ? "You can generate tasks with context from past exams."
                    : "Import PDFs using POST /api/import/exam-sheets"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ RAG health check failed");
            return StatusCode(500, new
            {
                status = "error",
                error = ex.Message
            });
        }
    }

    // ✅ NOWE - Zobacz przykładowe zadania z bazy
    [HttpGet("sample-tasks")]
    public async Task<ActionResult> GetSampleTasks(
        [FromQuery] string? level = null,
        [FromQuery] string? subject = null,
        [FromQuery] int limit = 3)
    {
        try
        {
            var tasks = await _ragService.SearchSimilarTasks(
                "",
                limit: limit,
                level: level,
                subject: subject
            );

            if (tasks.Count == 0)
            {
                return Ok(new
                {
                    message = "No tasks found in database. Import exam sheets first.",
                    recommendation = "Use POST /api/import/exam-sheets to import PDFs"
                });
            }

            return Ok(new
            {
                count = tasks.Count,
                tasks = tasks.Select(t => new
                {
                    t.Id,
                    t.TaskNumber,
                    t.ExamSheetName,
                    t.Year,
                    t.Level,
                    t.Subject,
                    t.Content,
                    t.Page
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching sample tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ✅ NOWE - Szukaj zadań po słowie kluczowym
    [HttpGet("search")]
    [Authorize]
    public async Task<ActionResult> SearchTasks(
        [FromQuery] string query,
        [FromQuery] string? level = null,
        [FromQuery] string? subject = null,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { error = "Query parameter is required" });
            }

            var results = await _ragService.SearchSimilarTasks(query, limit, level, subject);

            return Ok(new
            {
                query,
                resultsCount = results.Count,
                filters = new { level, subject },
                results = results.Select(t => new
                {
                    t.Id,
                    t.TaskNumber,
                    t.ExamSheetName,
                    t.Year,
                    t.Level,
                    t.Subject,
                    ContentPreview = t.Content.Length > 200
                        ? t.Content.Substring(0, 200) + "..."
                        : t.Content
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error searching tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}