using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

[Table("Reviews")]
public class Review
{
    [Key]
    [Column("Id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    public int UserId { get; set; }

    [Required]
    [Range(1, 5)]
    [Column("rating")]
    public int Rating { get; set; }  // 1-5 gwiazdek

    [Required]
    [MaxLength(500)]
    [Column("comment")]
    public string Comment { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_approved")]
    public bool IsApproved { get; set; } = true;  // Moderacja (opcjonalnie)

    // Navigation property
    [ForeignKey("UserId")]
    public User? User { get; set; }
}