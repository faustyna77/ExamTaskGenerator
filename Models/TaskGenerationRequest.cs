using System.ComponentModel.DataAnnotations;

namespace ExamCreateApp.Models;

public class TaskGenerationRequest
{
    public string? TaskTopic { get; set; }

    [Required]
    public string DifficultyLevel { get; set; } = "podstawowy"; // podstawowy/rozszerzony

    public string? PhysicsSubject { get; set; } // mechanika, elektryczność, optyka, etc.

    [Range(1, 10)]
    public int TaskCount { get; set; } = 1;
}

public class TaskGenerationResponse
{
    public bool Success { get; set; }
    public List<GeneratedTask> Tasks { get; set; } = new();
    public string? Message { get; set; }
}

public class GeneratedTask
{
    public string Content { get; set; } = string.Empty;
    public List<string>? Answers { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Solution { get; set; }
    public string? Source { get; set; }
}