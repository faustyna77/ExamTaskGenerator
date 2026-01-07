using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ExamCreateApp.Services;

namespace ExamCreateApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly StripeService _stripeService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(StripeService stripeService, ILogger<PaymentController> logger)
    {
        _stripeService = stripeService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.Parse(userIdClaim ?? "0");
    }

    // POST: api/payment/create-checkout
    [HttpPost("create-checkout")]
    [Authorize]
    public async Task<ActionResult> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var checkoutUrl = await _stripeService.CreateCheckoutSession(userId, request.PlanType);

            return Ok(new { checkoutUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating checkout");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // POST: api/payment/webhook
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<ActionResult> StripeWebhook()
    {
        try
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var stripeSignature = Request.Headers["Stripe-Signature"].ToString();

            await _stripeService.HandleWebhook(json, stripeSignature);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Webhook processing error");
            return BadRequest();
        }
    }
}

public class CreateCheckoutRequest
{
    public string PlanType { get; set; } = "monthly"; // "monthly" lub "yearly"
}