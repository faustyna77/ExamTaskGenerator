using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using ExamCreateApp.Models;
using ExamCreateApp.Services;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MockExamController : ControllerBase
{
    private readonly MockExamService _mockExamService;
    private readonly ILogger<MockExamController> _logger;

    public MockExamController(MockExamService mockExamService, ILogger<MockExamController> logger)
    {
        _mockExamService = mockExamService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // POST: api/mockexam/start
    [HttpPost("start")]
    public async Task<ActionResult> StartExam([FromBody] StartMockExamRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var exam = await _mockExamService.StartMockExam(userId, request);

            // Zwróć zadania do rozwiązania
            var tasks = JsonSerializer.Deserialize<List<GeneratedTaskItemDto>>(exam.TasksData);

            return Ok(new
            {
                examId = exam.Id,
                level = exam.Level,
                taskCount = exam.TaskCount,
                timeLimitMinutes = exam.TimeLimitMinutes,
                startedAt = exam.StartedAt,
                expiresAt = exam.StartedAt.AddMinutes(exam.TimeLimitMinutes),
                tasks = tasks?.Select((t, index) => new
                {
                    index,
                    content = t.Content,
                    answers = t.Answers
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error starting mock exam");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // POST: api/mockexam/{id}/submit
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<MockExamReportDto>> SubmitExam(int id, [FromBody] SubmitMockExamRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var report = await _mockExamService.SubmitMockExam(userId, id, request);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error submitting exam {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/mockexam/my
    [HttpGet("my")]
    public async Task<ActionResult> GetMyExams()
    {
        try
        {
            var userId = GetCurrentUserId();
            var exams = await _mockExamService.GetUserExams(userId);
            return Ok(exams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching exams");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}