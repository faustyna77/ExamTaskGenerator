using Stripe;
using Stripe.Checkout;
using ExamCreateApp.Data;

namespace ExamCreateApp.Services;

public class StripeService
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;
    private readonly SubscriptionService _subscriptionService;
    private readonly ILogger<StripeService> _logger;

    public StripeService(
        IConfiguration configuration,
        AppDbContext dbContext,
        SubscriptionService subscriptionService,
        ILogger<StripeService> logger)
    {
        _configuration = configuration;
        _dbContext = dbContext;
        _subscriptionService = subscriptionService;
        _logger = logger;

        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<string> CreateCheckoutSession(int userId, string planType)
    {
        var user = await _dbContext.Users.FindAsync(userId);

        if (user == null)
            throw new InvalidOperationException("User not found");

        // Cennik
        var prices = new Dictionary<string, (long amount, int months)>
        {
            { "monthly", (999, 1) },    // 9.99 PLN
            { "yearly", (9999, 12) }     // 99.99 PLN
        };

        if (!prices.ContainsKey(planType))
            throw new ArgumentException("Invalid plan type");

        var (amount, months) = prices[planType];

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "pln",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"ExamCreateApp Premium - {planType}",
                            Description = "Unlimited PDF downloads + premium features"
                        },
                        UnitAmount = amount,
                    },
                    Quantity = 1,
                },
            },
            Mode = "payment",
            SuccessUrl = "https://localhost:7013/payment/success?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = "https://localhost:7013/payment/cancel",
            ClientReferenceId = userId.ToString(),
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() },
                { "planType", planType },
                { "months", months.ToString() }
            }
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options);

        _logger.LogInformation($"💳 Created Stripe session for user {userId}: {session.Id}");

        return session.Url;
    }

    public async Task HandleWebhook(string json, string stripeSignature)
    {
        try
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];
            var stripeEvent = EventUtility.ConstructEvent(json, stripeSignature, webhookSecret);

            // ✅ POPRAWKA: Użyj stringa zamiast Events.CheckoutSessionCompleted
            if (stripeEvent.Type == "checkout.session.completed")
            {
                var session = stripeEvent.Data.Object as Session;

                if (session?.Metadata != null &&
                    session.Metadata.TryGetValue("userId", out var userIdStr) &&
                    session.Metadata.TryGetValue("months", out var monthsStr))
                {
                    var userId = int.Parse(userIdStr);
                    var months = int.Parse(monthsStr);

                    await _subscriptionService.GrantPremium(userId, months);

                    _logger.LogInformation($"✅ Webhook: User {userId} payment successful!");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Stripe webhook error");
            throw;
        }
    }
}