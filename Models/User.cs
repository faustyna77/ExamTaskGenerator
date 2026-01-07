using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ExamCreateApp.Models;

/// <summary>
/// User entity - represents a user in the database
/// </summary>
[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Column("password_hash")]
    [MaxLength(255)] // ✅ Dodano limit dla BCrypt hash
    public string PasswordHash { get; set; } = string.Empty;

    [Column("first_name")]
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [MaxLength(50)]
    public string? LastName { get; set; }

    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = UserRoles.User; // ✅ Użycie stałej zamiast stringa

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("last_login")]
    public DateTime? LastLogin { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("is_premium")]
    public bool IsPremium { get; set; } = false;

    [Column("premium_expires_at")]
    public DateTime? PremiumExpiresAt { get; set; }

    [Column("pdf_downloads_count")]
    public int PdfDownloadsCount { get; set; } = 0;

    [Column("stripe_customer_id")]
    public string? StripeCustomerId { get; set; }
}

/// <summary>
/// User roles constants
/// </summary>
public static class UserRoles
{
    public const string User = "user";
    public const string Admin = "admin";
    public const string Moderator = "moderator";
}