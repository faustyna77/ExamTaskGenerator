using System.ComponentModel.DataAnnotations;

namespace ExamCreateApp.Models;

public class StartMockExamRequest
{
    [Required]
    public string Level { get; set; } = "podstawowy";  // "podstawowy" | "rozszerzony"

    [Range(1, 50)]
    public int TaskCount { get; set; } = 40;

    [Range(30, 300)]
    public int TimeLimitMinutes { get; set; } = 150;

    public List<string>? Topics { get; set; }  // null = wszystkie tematy
}

public class SubmitMockExamRequest
{
    [Required]
    public Dictionary<int, string> Answers { get; set; } = new();
    // Przykład: { 0: "A", 1: "B", 2: "C", ... }
}

public class MockExamDto
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public int TimeLimitMinutes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? TimeElapsedSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? Score { get; set; }
    public double? Percentage { get; set; }
}

public class MockExamReportDto
{
    public int Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public int TimeElapsedSeconds { get; set; }
    public double Score { get; set; }
    public int MaxScore { get; set; }
    public double Percentage { get; set; }
    public List<TaskResultDto> TaskResults { get; set; } = new();
    public TopicPerformanceDto TopicPerformance { get; set; } = new();
}

public class TaskResultDto
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<string>? Answers { get; set; }
    public string? UserAnswer { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string Solution { get; set; } = string.Empty;
    public double PointsEarned { get; set; }
    public double MaxPoints { get; set; }
}

public class TopicPerformanceDto
{
    public Dictionary<string, double> ByTopic { get; set; } = new();
    // Przykład: { "mechanika": 85.5, "elektryczność": 70.0 }
}