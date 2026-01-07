using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

[Table("MockExams")]
public class MockExam
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("exam_type")]
    public string ExamType { get; set; } = "matura";  // "matura" | "custom"

    [Required]
    [Column("level")]
    public string Level { get; set; } = "rozszerzony";  // "podstawowy" | "rozszerzony"

    [Required]
    [Column("task_count")]
    public int TaskCount { get; set; } = 40;

    [Required]
    [Column("time_limit_minutes")]
    public int TimeLimitMinutes { get; set; } = 150;

    [Column("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [Column("time_elapsed_seconds")]
    public int? TimeElapsedSeconds { get; set; }

    [Required]
    [Column("status")]
    public string Status { get; set; } = "in_progress";  // "in_progress" | "completed" | "abandoned"

    [Column("tasks_data")]
    public string TasksData { get; set; } = "[]";  // JSON - wygenerowane zadania

    [Column("user_answers")]
    public string UserAnswers { get; set; } = "{}";  // JSON - odpowiedzi użytkownika

    [Column("score")]
    public double? Score { get; set; }

    [Column("max_score")]
    public int? MaxScore { get; set; }

    [Column("percentage")]
    public double? Percentage { get; set; }

    // Navigation
    [ForeignKey("UserId")]
    public User? User { get; set; }
}

[Table("MockExamAnswers")]
public class MockExamAnswer
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("mock_exam_id")]
    public int MockExamId { get; set; }

    [Required]
    [Column("task_index")]
    public int TaskIndex { get; set; }  // 0-39

    [Required]
    [Column("task_content")]
    public string TaskContent { get; set; } = string.Empty;

    [Column("user_answer")]
    public string? UserAnswer { get; set; }

    [Required]
    [Column("correct_answer")]
    public string CorrectAnswer { get; set; } = string.Empty;

    [Required]
    [Column("is_correct")]
    public bool IsCorrect { get; set; }

    [Column("points_earned")]
    public double PointsEarned { get; set; }

    [Column("max_points")]
    public double MaxPoints { get; set; } = 1;

    // Navigation
    [ForeignKey("MockExamId")]
    public MockExam? MockExam { get; set; }
}