using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ExamCreateApp.Data;
using ExamCreateApp.Models;

namespace ExamCreateApp.Services;

public class MockExamService
{
    private readonly AppDbContext _dbContext;
    private readonly GeminiService _geminiService;
    private readonly RAGService _ragService;
    private readonly ILogger<MockExamService> _logger;

    public MockExamService(
        AppDbContext dbContext,
        GeminiService geminiService,
        RAGService ragService,
        ILogger<MockExamService> logger)
    {
        _dbContext = dbContext;
        _geminiService = geminiService;
        _ragService = ragService;
        _logger = logger;
    }

    public async Task<MockExam> StartMockExam(int userId, StartMockExamRequest request)
    {
        _logger.LogInformation($"📝 User {userId} starting mock exam: {request.Level}, {request.TaskCount} tasks");

        var tasks = await GenerateExamTasks(request);

        var mockExam = new MockExam
        {
            UserId = userId,
            Level = request.Level,
            TaskCount = request.TaskCount,
            TimeLimitMinutes = request.TimeLimitMinutes,
            StartedAt = DateTime.UtcNow,
            Status = "in_progress",
            TasksData = JsonSerializer.Serialize(tasks),
            MaxScore = tasks.Count
        };

        await _dbContext.MockExams.AddAsync(mockExam);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"✅ Mock exam {mockExam.Id} started");

        return mockExam;
    }

    private async Task<List<GeneratedTaskItemDto>> GenerateExamTasks(StartMockExamRequest request)
    {
        _logger.LogInformation($"📚 Loading {request.TaskCount} tasks from database (level: {request.Level})");

        // ✅ POBIERZ Z BAZY ZAMIAST GENEROWAĆ (unikamy rate limits!)
        var tasksFromDB = await _dbContext.ExamTasks
            .Where(t => t.Level == request.Level)
            .OrderBy(x => Guid.NewGuid()) // Random
            .Take(request.TaskCount)
            .ToListAsync();

        if (tasksFromDB.Count == 0)
        {
            throw new InvalidOperationException($"No tasks in database for level: {request.Level}");
        }

        _logger.LogInformation($"✅ Loaded {tasksFromDB.Count} tasks from database");

        return tasksFromDB.Select(t => new GeneratedTaskItemDto
        {
            Content = t.Content,
            Answers = new List<string>
            {
                "A) Odpowiedź A",
                "B) Odpowiedź B",
                "C) Odpowiedź C",
                "D) Odpowiedź D"
            },
            CorrectAnswer = "A",
            Solution = "Rozwiązanie dostępne w arkuszu maturalnym.",
            Source = $"{t.ExamSheetName} ({t.Year})",
            PointsAvailable = 1
        }).ToList();
    }

    public async Task<MockExamReportDto> SubmitMockExam(int userId, int examId, SubmitMockExamRequest request)
    {
        var exam = await _dbContext.MockExams
            .FirstOrDefaultAsync(e => e.Id == examId && e.UserId == userId);

        if (exam == null)
            throw new InvalidOperationException("Exam not found");

        if (exam.Status != "in_progress")
            throw new InvalidOperationException("Exam already completed");

        var tasks = JsonSerializer.Deserialize<List<GeneratedTaskItemDto>>(exam.TasksData)
            ?? throw new InvalidOperationException("Invalid tasks data");

        var results = new List<TaskResultDto>();
        var totalScore = 0.0;

        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            var userAnswer = request.Answers.GetValueOrDefault(i);
            var isCorrect = userAnswer?.Trim().ToUpper() == task.CorrectAnswer?.Trim().ToUpper();
            var pointsEarned = isCorrect ? (task.PointsAvailable ?? 1) : 0;

            totalScore += pointsEarned;

            results.Add(new TaskResultDto
            {
                Index = i,
                Content = task.Content,
                Answers = task.Answers,
                UserAnswer = userAnswer,
                CorrectAnswer = task.CorrectAnswer ?? "",
                IsCorrect = isCorrect,
                Solution = task.Solution ?? "",
                PointsEarned = pointsEarned,
                MaxPoints = task.PointsAvailable ?? 1
            });

            await _dbContext.MockExamAnswers.AddAsync(new MockExamAnswer
            {
                MockExamId = examId,
                TaskIndex = i,
                TaskContent = task.Content,
                UserAnswer = userAnswer,
                CorrectAnswer = task.CorrectAnswer ?? "",
                IsCorrect = isCorrect,
                PointsEarned = pointsEarned,
                MaxPoints = task.PointsAvailable ?? 1
            });
        }

        exam.FinishedAt = DateTime.UtcNow;
        exam.TimeElapsedSeconds = (int)(exam.FinishedAt.Value - exam.StartedAt).TotalSeconds;
        exam.Status = "completed";
        exam.Score = totalScore;
        exam.Percentage = (totalScore / exam.MaxScore!.Value) * 100;
        exam.UserAnswers = JsonSerializer.Serialize(request.Answers);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"✅ Mock exam {examId} completed: {exam.Percentage:F1}%");

        return new MockExamReportDto
        {
            Id = exam.Id,
            Level = exam.Level,
            TaskCount = exam.TaskCount,
            StartedAt = exam.StartedAt,
            FinishedAt = exam.FinishedAt.Value,
            TimeElapsedSeconds = exam.TimeElapsedSeconds!.Value,
            Score = exam.Score!.Value,
            MaxScore = exam.MaxScore!.Value,
            Percentage = exam.Percentage!.Value,
            TaskResults = results,
            TopicPerformance = CalculateTopicPerformance(results)
        };
    }

    private TopicPerformanceDto CalculateTopicPerformance(List<TaskResultDto> results)
    {
        return new TopicPerformanceDto();
    }

    public async Task<List<MockExamDto>> GetUserExams(int userId)
    {
        return await _dbContext.MockExams
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => new MockExamDto
            {
                Id = e.Id,
                Level = e.Level,
                TaskCount = e.TaskCount,
                TimeLimitMinutes = e.TimeLimitMinutes,
                StartedAt = e.StartedAt,
                FinishedAt = e.FinishedAt,
                TimeElapsedSeconds = e.TimeElapsedSeconds,
                Status = e.Status,
                Score = e.Score,
                Percentage = e.Percentage
            })
            .ToListAsync();
    }
}