using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Microsoft.Data.SqlClient;
using ProjectTemplate.Api.Services;

namespace ProjectTemplate.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly StripeService _stripeService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IConfiguration config, StripeService stripeService, ILogger<PaymentsController> logger)
    {
        _config = config;
        _stripeService = stripeService;
        _logger = logger;
    }

    private SqlConnection CreateConnection() =>
        new(_config.GetConnectionString("DefaultConnection"));

    /// <summary>
    /// Returns the Stripe publishable key for the frontend.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            publishableKey = _config["Stripe:PublishableKey"],
            priceIds = new
            {
                pro = _config["Stripe:PriceIds:Pro"],
                business = _config["Stripe:PriceIds:Business"],
            }
        });
    }

    /// <summary>
    /// Creates a Stripe Checkout session and returns the URL.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize]
    public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Get user email
        using var conn = CreateConnection();
        var email = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT Email FROM Users WHERE Id = @Id", new { Id = userId });

        if (string.IsNullOrEmpty(email))
            return BadRequest(new { message = "User email not found" });

        try
        {
            var session = await _stripeService.CreateCheckoutSession(
                userId,
                email,
                request.PriceId,
                request.SuccessUrl,
                request.CancelUrl);

            return Ok(new { url = session.Url });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe checkout error for user {UserId}", userId);
            return BadRequest(new { message = "Failed to create checkout session" });
        }
    }

    /// <summary>
    /// Creates a Stripe Customer Portal session and returns the URL.
    /// </summary>
    [HttpPost("portal")]
    [Authorize]
    public async Task<IActionResult> CreatePortal([FromBody] PortalRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        using var conn = CreateConnection();
        var customerId = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT StripeCustomerId FROM Subscriptions WHERE UserId = @UserId",
            new { UserId = userId });

        if (string.IsNullOrEmpty(customerId))
            return BadRequest(new { message = "No subscription found" });

        try
        {
            var session = await _stripeService.CreatePortalSession(customerId, request.ReturnUrl);
            return Ok(new { url = session.Url });
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe portal error for user {UserId}", userId);
            return BadRequest(new { message = "Failed to create portal session" });
        }
    }

    /// <summary>
    /// Handles incoming Stripe webhook events.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
            return BadRequest(new { message = "Missing Stripe-Signature header" });

        try
        {
            await _stripeService.HandleWebhookEvent(json, signature);
            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed");
            return BadRequest(new { message = "Webhook signature verification failed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Gets the current user's subscription status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var status = await _stripeService.GetSubscriptionStatus(userId);

        if (status == null)
        {
            return Ok(new
            {
                plan = "Free",
                status = "inactive",
                stripeCustomerId = (string?)null,
                stripeSubscriptionId = (string?)null,
                currentPeriodEnd = (DateTime?)null,
            });
        }

        return Ok(new
        {
            plan = (string?)status.PlanName ?? "Free",
            status = (string)status.Status,
            stripeCustomerId = (string)status.StripeCustomerId,
            stripeSubscriptionId = (string?)status.StripeSubscriptionId,
            currentPeriodEnd = (DateTime?)status.CurrentPeriodEnd,
        });
    }
}

public class CheckoutRequest
{
    public string PriceId { get; set; } = string.Empty;
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public class PortalRequest
{
    public string ReturnUrl { get; set; } = string.Empty;
}
