using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ExamCreateApp.Data;
using ExamCreateApp.Models;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnswersController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AnswersController> _logger;

    public AnswersController(AppDbContext dbContext, ILogger<AnswersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // POST: api/answers/submit - Sprawdź odpowiedź użytkownika
    [HttpPost("submit")]
    public async Task<ActionResult> SubmitAnswer([FromBody] SubmitAnswerRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Pobierz zadanie z bazy
            var generatedTask = await _dbContext.GeneratedTasks
                .FirstOrDefaultAsync(t => t.Id == request.GeneratedTaskId && t.UserId == userId);

            if (generatedTask == null)
            {
                return NotFound(new { error = "Task not found" });
            }

            // Parsuj JSON zadania żeby wyciągnąć poprawną odpowiedź
            var taskData = JsonSerializer.Deserialize<TaskGenerationResponse>(
                generatedTask.GeneratedText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (taskData?.Tasks == null || taskData.Tasks.Count == 0)
            {
                return BadRequest(new { error = "Invalid task data" });
            }

            var task = taskData.Tasks[0]; // Zakładamy że to pierwsze zadanie
            var isCorrect = request.UserAnswer.Trim().ToUpper() == task.CorrectAnswer?.Trim().ToUpper();

            // Zapisz odpowiedź
            var userAnswer = new UserAnswer
            {
                UserId = userId,
                GeneratedTaskId = request.GeneratedTaskId,
                TaskContent = task.Content ?? "",
                UserAnswerText = request.UserAnswer,
                CorrectAnswer = task.CorrectAnswer ?? "",
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.UtcNow
            };

            await _dbContext.UserAnswers.AddAsync(userAnswer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✅ User {userId} answered task {request.GeneratedTaskId}: {(isCorrect ? "CORRECT" : "WRONG")}");

            return Ok(new
            {
                success = true,
                isCorrect,
                correctAnswer = task.CorrectAnswer,
                solution = task.Solution,
                userAnswer = request.UserAnswer
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error submitting answer");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/answers/my-stats - Statystyki użytkownika
    [HttpGet("my-stats")]
    public async Task<ActionResult> GetMyStatistics()
    {
        try
        {
            var userId = GetCurrentUserId();

            var totalAnswers = await _dbContext.UserAnswers
                .Where(a => a.UserId == userId)
                .CountAsync();

            var correctAnswers = await _dbContext.UserAnswers
                .Where(a => a.UserId == userId && a.IsCorrect)
                .CountAsync();

            var wrongAnswers = totalAnswers - correctAnswers;

            var accuracy = totalAnswers > 0 ? (correctAnswers / (double)totalAnswers) * 100 : 0;

            return Ok(new
            {
                totalAnswers,
                correctAnswers,
                wrongAnswers,
                accuracy = Math.Round(accuracy, 2)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching stats");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/answers/history - Historia odpowiedzi
    [HttpGet("history")]
    public async Task<ActionResult> GetAnswerHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();

            var answers = await _dbContext.UserAnswers
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.AnsweredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.TaskContent,
                    a.UserAnswerText,
                    a.CorrectAnswer,
                    a.IsCorrect,
                    a.AnsweredAt
                })
                .ToListAsync();

            var totalCount = await _dbContext.UserAnswers
                .Where(a => a.UserId == userId)
                .CountAsync();

            return Ok(new
            {
                answers,
                totalCount,
                page,
                pageSize
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching history");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}