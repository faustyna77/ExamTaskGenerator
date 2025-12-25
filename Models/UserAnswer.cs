using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

[Table("UserAnswers")]
public class UserAnswer
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Column("generated_task_id")]
    public int GeneratedTaskId { get; set; }

    [Required]
    [Column("task_content")]
    public string TaskContent { get; set; } = string.Empty;

    [Required]
    [Column("user_answer")]
    public string UserAnswerText { get; set; } = string.Empty; // A, B, C, D

    [Required]
    [Column("correct_answer")]
    public string CorrectAnswer { get; set; } = string.Empty;

    [Required]
    [Column("is_correct")]
    public bool IsCorrect { get; set; }

    [Column("answered_at")]
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User? User { get; set; }

    [ForeignKey("GeneratedTaskId")]
    public GeneratedTask? GeneratedTask { get; set; }
}