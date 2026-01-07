using System.ComponentModel.DataAnnotations;

namespace ExamCreateApp.Models;

public class TaskGenerationRequest
{
    public string? TaskTopic { get; set; }

    [Required]
    public string DifficultyLevel { get; set; } = "podstawowy";

    public string? PhysicsSubject { get; set; }

    [Range(1, 10)]
    public int TaskCount { get; set; } = 1;
    public string TaskType { get; set; } = "closed";
}

public class TaskGenerationResponse
{
    public bool Success { get; set; }
    public List<GeneratedTaskItemDto> Tasks { get; set; } = new();  // ✅ ZMIEŃ NAZWĘ!
    public string? Message { get; set; }
}

// ✅ ZMIEŃ NAZWĘ: GeneratedTaskDto → GeneratedTaskItemDto
public class GeneratedTaskItemDto  // ← NOWA NAZWA!
{
    public string Content { get; set; } = string.Empty;
    public List<string>? Answers { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Solution { get; set; }
    public string? Source { get; set; }
    
    public int? PointsAvailable { get; set; }
}