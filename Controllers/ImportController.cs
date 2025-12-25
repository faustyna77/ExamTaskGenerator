using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ExamCreateApp.Services;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly PdfImportService _importService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(PdfImportService importService, ILogger<ImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    [HttpPost("exam-sheets")]
   
    public async Task<IActionResult> ImportExamSheets()
    {
        try
        {
            _logger.LogInformation("🚀 Starting PDF import...");
            var count = await _importService.ImportExamSheetsFromPdfs();

            return Ok(new
            {
                success = true,
                message = $"Successfully imported {count} tasks",
                tasksImported = count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Import failed");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}