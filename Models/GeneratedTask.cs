using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

[Table("GeneratedTasks")]
public class GeneratedTask
{
    [Key]
    [Column("Id")]  // ✅ Wielka litera!
    public int Id { get; set; }

    [Required]
    [Column("user_id")]  // ✅ snake_case
    public int UserId { get; set; }

    [Required]
    [Column("prompt")]  // ✅ lowercase
    public string Prompt { get; set; } = string.Empty;

    [Required]
    [Column("generated_text")]  // ✅ snake_case
    public string GeneratedText { get; set; } = string.Empty;

    [Column("created_at")]  // ✅ snake_case
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public User? User { get; set; }
}