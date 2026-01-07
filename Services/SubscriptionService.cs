using ExamCreateApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamCreateApp.Services;

public class SubscriptionService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SubscriptionService> _logger;
    private const int FREE_PDF_LIMIT = 3;

    public SubscriptionService(AppDbContext dbContext, ILogger<SubscriptionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> CanDownloadPdf(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);

        if (user == null)
            return false;

        // Premium użytkownicy - bez limitu
        if (user.IsPremium && user.PremiumExpiresAt > DateTime.UtcNow)
        {
            return true;
        }

        // Free użytkownicy - sprawdź limit
        return user.PdfDownloadsCount < FREE_PDF_LIMIT;
    }

    public async Task<bool> IncrementPdfDownload(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);

        if (user == null)
            return false;

        user.PdfDownloadsCount++;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"📊 User {userId} downloaded PDF ({user.PdfDownloadsCount}/{FREE_PDF_LIMIT})");

        return true;
    }

    public async Task<int> GetRemainingDownloads(int userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);

        if (user == null)
            return 0;

        if (user.IsPremium && user.PremiumExpiresAt > DateTime.UtcNow)
        {
            return int.MaxValue; // Unlimited
        }

        return Math.Max(0, FREE_PDF_LIMIT - user.PdfDownloadsCount);
    }

    public async Task GrantPremium(int userId, int months)
    {
        var user = await _dbContext.Users.FindAsync(userId);

        if (user == null)
            return;

        user.IsPremium = true;
        user.PremiumExpiresAt = DateTime.UtcNow.AddMonths(months);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation($"✨ User {userId} granted premium for {months} months");
    }
}