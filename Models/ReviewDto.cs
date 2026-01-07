using System.ComponentModel.DataAnnotations;

namespace ExamCreateApp.Models;

// Request do tworzenia recenzji
public class CreateReviewRequest
{
    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int Rating { get; set; }

    [Required]
    [MinLength(10, ErrorMessage = "Comment must be at least 10 characters")]
    [MaxLength(500, ErrorMessage = "Comment cannot exceed 500 characters")]
    public string Comment { get; set; } = string.Empty;
}

// Response z recenzją
public class ReviewDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Statystyki
public class ReviewStatsDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingDistribution { get; set; } = new();
    // Przykład: { 5: 10, 4: 5, 3: 2, 2: 1, 1: 0 }
}