using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ExamCreateApp.Data;
using ExamCreateApp.Models;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(AppDbContext dbContext, ILogger<ReviewsController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // POST: api/reviews - Dodaj recenzję
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            // Sprawdź czy użytkownik już dodał recenzję
            var existingReview = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (existingReview != null)
            {
                return BadRequest(new
                {
                    error = "Review already exists",
                    message = "You have already submitted a review. You can edit your existing review instead.",
                    existingReviewId = existingReview.Id
                });
            }

            var review = new Review
            {
                UserId = userId,
                Rating = request.Rating,
                Comment = request.Comment,
                CreatedAt = DateTime.UtcNow,
                IsApproved = true
            };

            await _dbContext.Reviews.AddAsync(review);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"⭐ User {userId} added review with {request.Rating} stars");

            var user = await _dbContext.Users.FindAsync(userId);

            return CreatedAtAction(nameof(GetMyReview), new
            {
                id = review.Id,
                userId = review.UserId,
                username = user?.Username ?? "Anonymous",
                rating = review.Rating,
                comment = review.Comment,
                createdAt = review.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating review");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/reviews/my - Moja recenzja
    [HttpGet("my")]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> GetMyReview()
    {
        try
        {
            var userId = GetCurrentUserId();

            var review = await _dbContext.Reviews
                .Include(r => r.User)
                .Where(r => r.UserId == userId)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User!.Username,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (review == null)
            {
                return NotFound(new { message = "You haven't submitted a review yet" });
            }

            return Ok(review);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching user review");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // PUT: api/reviews/my - Edytuj recenzję
    [HttpPut("my")]
    [Authorize]
    public async Task<ActionResult<ReviewDto>> UpdateMyReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var review = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (review == null)
            {
                return NotFound(new { error = "Review not found" });
            }

            review.Rating = request.Rating;
            review.Comment = request.Comment;
            review.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"✏️ User {userId} updated review to {request.Rating} stars");

            var user = await _dbContext.Users.FindAsync(userId);

            return Ok(new
            {
                id = review.Id,
                userId = review.UserId,
                username = user?.Username ?? "Anonymous",
                rating = review.Rating,
                comment = review.Comment,
                createdAt = review.CreatedAt,
                updatedAt = review.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error updating review");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // DELETE: api/reviews/my - Usuń moją recenzję
    [HttpDelete("my")]
    [Authorize]
    public async Task<ActionResult> DeleteMyReview()
    {
        try
        {
            var userId = GetCurrentUserId();

            var review = await _dbContext.Reviews
                .FirstOrDefaultAsync(r => r.UserId == userId);

            if (review == null)
            {
                return NotFound(new { error = "Review not found" });
            }

            _dbContext.Reviews.Remove(review);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"🗑️ User {userId} deleted their review");

            return Ok(new { success = true, message = "Review deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting review");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/reviews - Wszystkie recenzje (paginacja)
    [HttpGet]
    public async Task<ActionResult<object>> GetAllReviews(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? minRating = null,
        [FromQuery] string sortBy = "recent") // "recent" | "highest" | "lowest"
    {
        try
        {
            var query = _dbContext.Reviews
                .Include(r => r.User)
                .Where(r => r.IsApproved);

            // Filtruj po ratingu
            if (minRating.HasValue)
            {
                query = query.Where(r => r.Rating >= minRating.Value);
            }

            // Sortowanie
            query = sortBy.ToLower() switch
            {
                "highest" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                "lowest" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                _ => query.OrderByDescending(r => r.CreatedAt) // "recent" (default)
            };

            var totalCount = await query.CountAsync();

            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new ReviewDto
                {
                    Id = r.Id,
                    UserId = r.UserId,
                    Username = r.User!.Username,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                reviews,
                totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching reviews");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // GET: api/reviews/stats - Statystyki recenzji
    [HttpGet("stats")]
    public async Task<ActionResult<ReviewStatsDto>> GetReviewStats()
    {
        try
        {
            var reviews = await _dbContext.Reviews
                .Where(r => r.IsApproved)
                .ToListAsync();

            if (reviews.Count == 0)
            {
                return Ok(new ReviewStatsDto
                {
                    AverageRating = 0,
                    TotalReviews = 0,
                    RatingDistribution = new Dictionary<int, int>
                    {
                        { 5, 0 }, { 4, 0 }, { 3, 0 }, { 2, 0 }, { 1, 0 }
                    }
                });
            }

            var averageRating = reviews.Average(r => r.Rating);
            var distribution = reviews
                .GroupBy(r => r.Rating)
                .ToDictionary(g => g.Key, g => g.Count());

            // Dodaj brakujące oceny (0 recenzji)
            for (int i = 1; i <= 5; i++)
            {
                if (!distribution.ContainsKey(i))
                    distribution[i] = 0;
            }

            return Ok(new ReviewStatsDto
            {
                AverageRating = Math.Round(averageRating, 2),
                TotalReviews = reviews.Count,
                RatingDistribution = distribution.OrderByDescending(x => x.Key)
                    .ToDictionary(x => x.Key, x => x.Value)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error fetching review stats");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // DELETE: api/reviews/{id} - Admin usuwa recenzję
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteReview(int id)
    {
        try
        {
            var review = await _dbContext.Reviews.FindAsync(id);

            if (review == null)
            {
                return NotFound(new { error = "Review not found" });
            }

            _dbContext.Reviews.Remove(review);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation($"🗑️ Admin deleted review {id}");

            return Ok(new { success = true, message = "Review deleted by admin" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error deleting review");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}