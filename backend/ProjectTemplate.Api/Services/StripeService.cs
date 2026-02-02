using Dapper;
using Microsoft.Data.SqlClient;
using Stripe;
using Stripe.Checkout;

namespace ProjectTemplate.Api.Services;

public class StripeService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StripeService> _logger;

    public StripeService(IConfiguration config, ILogger<StripeService> logger)
    {
        _config = config;
        _logger = logger;

        StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
    }

    private SqlConnection CreateConnection() =>
        new(_config.GetConnectionString("DefaultConnection"));

    /// <summary>
    /// Creates a Stripe Checkout session for a subscription.
    /// </summary>
    public async Task<Session> CreateCheckoutSession(int userId, string email, string priceId, string successUrl, string cancelUrl)
    {
        // Check if user already has a Stripe customer
        var customerId = await GetOrCreateStripeCustomer(userId, email);

        var options = new SessionCreateOptions
        {
            Customer = customerId,
            PaymentMethodTypes = new List<string> { "card" },
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1,
                }
            },
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() }
            }
        };

        var service = new SessionService();
        return await service.CreateAsync(options);
    }

    /// <summary>
    /// Creates a Stripe Customer Portal session for self-service billing management.
    /// </summary>
    public async Task<Stripe.BillingPortal.Session> CreatePortalSession(string customerId, string returnUrl)
    {
        var options = new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        };

        var service = new Stripe.BillingPortal.SessionService();
        return await service.CreateAsync(options);
    }

    /// <summary>
    /// Processes Stripe webhook events.
    /// </summary>
    public async Task HandleWebhookEvent(string json, string signature)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"];
        var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

        _logger.LogInformation("Processing Stripe webhook: {EventType}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompleted(stripeEvent);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionUpdated(stripeEvent);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionDeleted(stripeEvent);
                break;
            case "invoice.payment_failed":
                await HandlePaymentFailed(stripeEvent);
                break;
            default:
                _logger.LogInformation("Unhandled event type: {EventType}", stripeEvent.Type);
                break;
        }
    }

    /// <summary>
    /// Gets the subscription status for a user.
    /// </summary>
    public async Task<dynamic?> GetSubscriptionStatus(int userId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync(
            @"SELECT StripeCustomerId, StripeSubscriptionId, PlanName, Status, CurrentPeriodEnd
              FROM Subscriptions WHERE UserId = @UserId",
            new { UserId = userId });
    }

    /// <summary>
    /// Gets or creates a Stripe customer, linked to a local user.
    /// </summary>
    private async Task<string> GetOrCreateStripeCustomer(int userId, string email)
    {
        using var conn = CreateConnection();

        // Check for existing subscription record
        var existing = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT StripeCustomerId FROM Subscriptions WHERE UserId = @UserId",
            new { UserId = userId });

        if (!string.IsNullOrEmpty(existing))
            return existing;

        // Create new Stripe customer
        var customerService = new CustomerService();
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() }
            }
        });

        // Create subscription record with inactive status
        await conn.ExecuteAsync(
            @"INSERT INTO Subscriptions (UserId, StripeCustomerId, Status)
              VALUES (@UserId, @StripeCustomerId, 'inactive')",
            new { UserId = userId, StripeCustomerId = customer.Id });

        return customer.Id;
    }

    private async Task HandleCheckoutCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session;
        if (session == null) return;

        var customerId = session.CustomerId;
        var subscriptionId = session.SubscriptionId;

        // Get subscription details to determine plan
        var subscriptionService = new SubscriptionService();
        var subscription = await subscriptionService.GetAsync(subscriptionId);
        var priceId = subscription.Items.Data[0].Price.Id;
        var planName = ResolvePlanName(priceId);

        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Subscriptions SET
                StripeSubscriptionId = @SubscriptionId,
                PlanName = @PlanName,
                Status = 'active',
                CurrentPeriodEnd = @PeriodEnd,
                UpdatedAt = GETUTCDATE()
              WHERE StripeCustomerId = @CustomerId",
            new
            {
                SubscriptionId = subscriptionId,
                PlanName = planName,
                PeriodEnd = subscription.CurrentPeriodEnd,
                CustomerId = customerId
            });

        _logger.LogInformation("Checkout completed for customer {CustomerId}, plan: {Plan}", customerId, planName);
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        var priceId = subscription.Items.Data[0].Price.Id;
        var planName = ResolvePlanName(priceId);
        var status = MapSubscriptionStatus(subscription.Status);

        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Subscriptions SET
                PlanName = @PlanName,
                Status = @Status,
                CurrentPeriodEnd = @PeriodEnd,
                UpdatedAt = GETUTCDATE()
              WHERE StripeCustomerId = @CustomerId",
            new
            {
                PlanName = planName,
                Status = status,
                PeriodEnd = subscription.CurrentPeriodEnd,
                CustomerId = subscription.CustomerId
            });

        _logger.LogInformation("Subscription updated for customer {CustomerId}: {Status}", subscription.CustomerId, status);
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription == null) return;

        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Subscriptions SET
                Status = 'canceled',
                UpdatedAt = GETUTCDATE()
              WHERE StripeCustomerId = @CustomerId",
            new { CustomerId = subscription.CustomerId });

        _logger.LogInformation("Subscription canceled for customer {CustomerId}", subscription.CustomerId);
    }

    private async Task HandlePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;

        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE Subscriptions SET
                Status = 'past_due',
                UpdatedAt = GETUTCDATE()
              WHERE StripeCustomerId = @CustomerId",
            new { CustomerId = invoice.CustomerId });

        _logger.LogWarning("Payment failed for customer {CustomerId}", invoice.CustomerId);
    }

    private string ResolvePlanName(string priceId)
    {
        var proPriceId = _config["Stripe:PriceIds:Pro"];
        var businessPriceId = _config["Stripe:PriceIds:Business"];

        if (priceId == proPriceId) return "Pro";
        if (priceId == businessPriceId) return "Business";
        return "Unknown";
    }

    private static string MapSubscriptionStatus(string stripeStatus)
    {
        return stripeStatus switch
        {
            "active" => "active",
            "past_due" => "past_due",
            "canceled" => "canceled",
            "unpaid" => "past_due",
            "trialing" => "active",
            _ => "inactive"
        };
    }
}
