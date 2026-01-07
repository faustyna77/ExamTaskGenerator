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
        _logger.LogInformation($"🔍 RAG Search: query='{query}', level={level}, subject={subject}");

        var dbQuery = _dbContext.ExamTasks.AsQueryable();

        // ✅ POPRAWKA 1: Filtruj tylko po level (ignoruj subject)
        if (!string.IsNullOrEmpty(level))
        {
            dbQuery = dbQuery.Where(t => t.Level == level);
        }

        // ✅ POPRAWKA 2: Subject ignorujemy - wszystkie zadania to "fizyka"
        // W przyszłości można dodać semantic search po content

        // ✅ POPRAWKA 3: Dodaj sortowanie (usunie warning o Skip/Take bez OrderBy)
        var results = await dbQuery
            .OrderByDescending(t => t.Year)
            .ThenBy(t => t.Page)
            .Take(limit)
            .ToListAsync();

        _logger.LogInformation($"📊 Found {results.Count} tasks");

        return results;
    }

    public string BuildContextFromExamSheets(List<ExamTask> similarTasks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== EXAMPLE TASKS FROM PAST EXAMS ===\n");

        foreach (var task in similarTasks)
        {
            // ✅ POPRAWKA: Dodaj "Year:" żeby regex w GeminiService mógł wyciągnąć rok
            sb.AppendLine($"[Source: {task.ExamSheetName}, Task {task.TaskNumber}, Year: {task.Year ?? 2024}, Level: {task.Level}]");
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