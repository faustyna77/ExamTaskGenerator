using Microsoft.EntityFrameworkCore;
using Pgvector;
using System.Text;
using System.Text.Json;
using ExamCreateApp.Data;
using ExamCreateApp.Models;

namespace ExamCreateApp.Services;

public class RAGService
{
    private readonly HttpClient _httpClient;
    private readonly string _geminiApiKey;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RAGService> _logger;
    private const string EmbeddingModel = "models/text-embedding-004";

    public RAGService(
        IConfiguration configuration,
        HttpClient httpClient,
        AppDbContext dbContext,
        ILogger<RAGService> logger)
    {
        _httpClient = httpClient;
        _geminiApiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Missing Gemini API Key");
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<ExamTask>> SearchSimilarTasks(
        string query,
        int limit = 5,
        string? level = null,
        string? subject = null)
    {
        // Temporary: return empty list until we implement full RAG
        _logger.LogInformation($"Searching for similar tasks: {query}");

        var dbQuery = _dbContext.ExamTasks.AsQueryable();

        if (!string.IsNullOrEmpty(level))
            dbQuery = dbQuery.Where(t => t.Level == level);

        if (!string.IsNullOrEmpty(subject))
            dbQuery = dbQuery.Where(t => t.Subject == subject);

        return await dbQuery.Take(limit).ToListAsync();
    }

    public string BuildContextFromExamSheets(List<ExamTask> similarTasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== EXAMPLE TASKS FROM PAST EXAMS ===\n");

        foreach (var task in similarTasks)
        {
            sb.AppendLine($"[Source: {task.ExamSheetName}, Task {task.TaskNumber}, Year: {task.Year ?? 2024}, Level: {task.Level}]");
            //                                                                        ^^^^^ DODAJ "Year:"
            sb.AppendLine(task.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task<object> GetStatistics()
    {
        return new
        {
            TotalTasks = await _dbContext.ExamTasks.CountAsync(),
            Years = await _dbContext.ExamTasks
                .Where(t => t.Year.HasValue)
                .Select(t => t.Year!.Value)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync(),
            Levels = await _dbContext.ExamTasks
                .GroupBy(t => t.Level)
                .Select(g => new { Level = g.Key, Count = g.Count() })
                .ToListAsync(),
            Subjects = await _dbContext.ExamTasks
                .Where(t => t.Subject != null)
                .GroupBy(t => t.Subject)
                .Select(g => new { Subject = g.Key, Count = g.Count() })
                .ToListAsync()
        };
    }
}