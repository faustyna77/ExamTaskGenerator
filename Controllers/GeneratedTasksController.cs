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
    private readonly SubscriptionService _subscriptionService;  // ✅ DODANE

    public GeneratedTasksController(
        AppDbContext dbContext,
        ILogger<GeneratedTasksController> logger,
        SubscriptionService subscriptionService)  // ✅ DODANE
    {
        _dbContext = dbContext;
        _logger = logger;
        _subscriptionService = subscriptionService;  // ✅ DODANE
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    [HttpGet]
    public async Task<ActionResult<List<GeneratedTaskDto>>> GetMyGeneratedTasks(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? search = null,              // W - Wyszukiwanie
    [FromQuery] string? sortBy = "createdAt",       // S - Sortowanie
    [FromQuery] string? sortOrder = "desc",         // S - Kierunek
    [FromQuery] string? level = null,               // ✅ F - Filtr poziom
    [FromQuery] string? subject = null,             // ✅ F - Filtr dział
    [FromQuery] string? dateFilter = null)          // ✅ F - Filtr data
    {
        try
        {
            var userId = GetCurrentUserId();

            var query = _dbContext.GeneratedTasks
                .Where(t => t.UserId == userId);

            // ✅ W - WYSZUKIWANIE (po treści)
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(t =>
                    t.Prompt.Contains(search) ||
                    t.GeneratedText.Contains(search)
                );
            }

            // ✅ F - FILTROWANIE PO POZIOMIE
            if (!string.IsNullOrEmpty(level))
            {
                query = query.Where(t => t.Prompt.Contains(level));
            }

            // ✅ F - FILTROWANIE PO DZIALE
            if (!string.IsNullOrEmpty(subject))
            {
                query = query.Where(t => t.Prompt.Contains(subject));
            }

            // ✅ F - FILTROWANIE PO DACIE
            if (!string.IsNullOrEmpty(dateFilter))
            {
                var now = DateTime.UtcNow;
                query = dateFilter.ToLower() switch
                {
                    "today" => query.Where(t => t.CreatedAt.Date == now.Date),
                    "week" => query.Where(t => t.CreatedAt >= now.AddDays(-7)),
                    "month" => query.Where(t => t.CreatedAt >= now.AddMonths(-1)),
                    _ => query
                };
            }

            // ✅ S - SORTOWANIE
            query = sortBy?.ToLower() switch
            {
                "prompt" => sortOrder == "asc"
                    ? query.OrderBy(t => t.Prompt)
                    : query.OrderByDescending(t => t.Prompt),
                "createdat" => sortOrder == "asc"
                    ? query.OrderBy(t => t.CreatedAt)
                    : query.OrderByDescending(t => t.CreatedAt),
                _ => query.OrderByDescending(t => t.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            // ✅ P - PAGINACJA
            var tasks = await query
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

    // GET: api/generatedtasks/{id}/export-pdf - Eksport do PDF
    [HttpGet("{id}/export-pdf")]
    public async Task<ActionResult> ExportToPdf(
    int id,
    [FromQuery] bool includeSolutions = true)
    {
        try
        {
            _logger.LogInformation($"📄 Step 1: Starting PDF export for task {id}");

            var userId = GetCurrentUserId();
            _logger.LogInformation($"📄 Step 2: User ID = {userId}");

            _logger.LogInformation($"📄 Step 3: Checking limit...");
            var canDownload = await _subscriptionService.CanDownloadPdf(userId);
            _logger.LogInformation($"📄 Step 4: Can download = {canDownload}");

            if (!canDownload)
            {
                _logger.LogWarning($"⚠️ User {userId} exceeded PDF limit");
                var remaining = await _subscriptionService.GetRemainingDownloads(userId);

                return StatusCode(403, new
                {
                    error = "PDF download limit exceeded",
                    message = "You've reached the free limit of 3 PDF downloads. Please upgrade to premium.",
                    remainingDownloads = remaining,
                    requiresPremium = true
                });
            }

            _logger.LogInformation($"📄 Step 5: Fetching task...");
            var task = await _dbContext.GeneratedTasks
                .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

            if (task == null)
            {
                _logger.LogWarning($"⚠️ Task {id} not found");
                return NotFound(new { error = "Task not found" });
            }

            _logger.LogInformation($"📄 Step 6: Getting PDF service...");
            var pdfService = HttpContext.RequestServices.GetRequiredService<PdfExportService>();

            _logger.LogInformation($"📄 Step 7: Generating PDF...");
            byte[] pdfBytes = includeSolutions
                ? pdfService.GeneratePdfFromTask(task)
                : pdfService.GeneratePdfWithoutSolutions(task);

            _logger.LogInformation($"✅ Step 8: PDF generated ({pdfBytes.Length} bytes)");

            _logger.LogInformation($"📄 Step 9: Incrementing counter...");
            await _subscriptionService.IncrementPdfDownload(userId);

            _logger.LogInformation($"✅ Step 10: Done!");

            var fileName = $"zadanie_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ FATAL ERROR in ExportToPdf");
            _logger.LogError($"❌ Type: {ex.GetType().FullName}");
            _logger.LogError($"❌ Message: {ex.Message}");
            _logger.LogError($"❌ Stack: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                _logger.LogError($"❌ Inner: {ex.InnerException.Message}");
                _logger.LogError($"❌ Inner Stack: {ex.InnerException.StackTrace}");
            }

            return StatusCode(500, new
            {
                error = "Failed to generate PDF",
                message = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    // GET: api/generatedtasks/pdf-limit-status - Status limitu PDF
    [HttpGet("pdf-limit-status")]
    public async Task<ActionResult> GetPdfLimitStatus()
    {
        try
        {
            var userId = GetCurrentUserId();
            var remaining = await _subscriptionService.GetRemainingDownloads(userId);
            var user = await _dbContext.Users.FindAsync(userId);

            return Ok(new
            {
                isPremium = user?.IsPremium ?? false,
                premiumExpiresAt = user?.PremiumExpiresAt,
                remainingDownloads = remaining,
                totalDownloads = user?.PdfDownloadsCount ?? 0,
                freeLimit = 3
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error getting PDF limit status");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}