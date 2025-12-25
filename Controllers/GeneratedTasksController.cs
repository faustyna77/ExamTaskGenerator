using ExamCreateApp.Data;
using ExamCreateApp.Models;
using ExamCreateApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeneratedTasksController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GeneratedTasksController> _logger;

    public GeneratedTasksController(AppDbContext dbContext, ILogger<GeneratedTasksController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // GET: api/generatedtasks - Lista wszystkich wygenerowanych zadań użytkownika
    [HttpGet]
    public async Task<ActionResult<List<GeneratedTaskDto>>> GetMyGeneratedTasks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = GetCurrentUserId();

            var tasks = await _dbContext.GeneratedTasks
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new GeneratedTaskDto
                {
                    Id = t.Id,
                    Prompt = t.Prompt,
                    GeneratedText = t.GeneratedText,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            var totalCount = await _dbContext.GeneratedTasks
                .Where(t => t.UserId == userId)
                .CountAsync();

            return Ok(new
            {
                tasks,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching generated tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/generatedtasks/{id} - Pojedyncze wygenerowane zadanie
    [HttpGet("{id}")]
    public async Task<ActionResult<GeneratedTaskDto>> GetGeneratedTask(int id)
    {
        try
        {
            var userId = GetCurrentUserId();

            var task = await _dbContext.GeneratedTasks
                .Where(t => t.Id == id && t.UserId == userId)
                .Select(t => new GeneratedTaskDto
                {
                    Id = t.Id,
                    Prompt = t.Prompt,
                    GeneratedText = t.GeneratedText,
                    CreatedAt = t.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (task == null)
            {
                return NotFound(new { error = "Task not found or access denied" });
            }

            return Ok(task);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error fetching task {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // DELETE: api/generatedtasks/{id} - Usuń wygenerowane zadanie
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGeneratedTask(int id)
    {
        try
        {
            var userId = GetCurrentUserId();

            var task = await _dbContext.GeneratedTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null)
            {
                return NotFound(new { error = "Task not found or access denied" });
            }

            _dbContext.GeneratedTasks.Remove(task);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"🗑️ User {userId} deleted task {id}");

            return Ok(new { success = true, message = "Task deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error deleting task {id}");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{id}/export-pdf")]
    public async Task<ActionResult> ExportToPdf(
    int id,
    [FromQuery] bool includeSolutions = true)
    {
        try
        {
            var userId = GetCurrentUserId();

            var task = await _dbContext.GeneratedTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null)
            {
                return NotFound(new { error = "Task not found" });
            }

            var pdfService = HttpContext.RequestServices.GetRequiredService<PdfExportService>();

            byte[] pdfBytes = includeSolutions
                ? pdfService.GeneratePdfFromTask(task)
                : pdfService.GeneratePdfWithoutSolutions(task);

            var fileName = $"zadanie_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error exporting task {id} to PDF");
            return StatusCode(500, new { error = ex.Message });
        }
    }
    // DELETE: api/generatedtasks/bulk - Usuń wiele zadań
    [HttpDelete("bulk")]
    public async Task<ActionResult> DeleteMultipleTasks([FromBody] DeleteMultipleRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var tasks = await _dbContext.GeneratedTasks
                .Where(t => request.Ids.Contains(t.Id) && t.UserId == userId)
                .ToListAsync();

            if (tasks.Count == 0)
            {
                return NotFound(new { error = "No tasks found" });
            }

            _dbContext.GeneratedTasks.RemoveRange(tasks);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"🗑️ User {userId} deleted {tasks.Count} tasks");

            return Ok(new
            {
                success = true,
                message = $"Deleted {tasks.Count} tasks",
                deletedCount = tasks.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting multiple tasks");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}