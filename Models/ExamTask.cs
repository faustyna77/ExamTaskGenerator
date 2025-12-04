using Pgvector;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

[Table("exam_tasks")]
public class ExamTask
{
    [Key]
    public int Id { get; set; }

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("task_number")]
    public string TaskNumber { get; set; } = string.Empty;

    [Column("exam_sheet_name")]
    public string ExamSheetName { get; set; } = string.Empty;

    [Column("year")]
    public int? Year { get; set; }

    [Column("level")]
    public string Level { get; set; } = string.Empty; // podstawowy/rozszerzony

    [Column("subject")]
    public string? Subject { get; set; } // mechanika, elektryczność, etc.

    [Column("page")]
    public int Page { get; set; }

    [Column("embedding")]
    public Vector? Embedding { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}