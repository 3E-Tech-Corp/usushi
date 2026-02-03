using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProjectTemplate.Api.Models;
using ProjectTemplate.Api.Services;

namespace ProjectTemplate.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class MealsController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ReceiptService _receiptService;
    private readonly SmsService _smsService;
    private readonly ILogger<MealsController> _logger;
    private readonly IWebHostEnvironment _env;

    public MealsController(IConfiguration config, ReceiptService receiptService, SmsService smsService, ILogger<MealsController> logger, IWebHostEnvironment env)
    {
        _config = config;
        _receiptService = receiptService;
        _smsService = smsService;
        _logger = logger;
        _env = env;
    }

    private SqlConnection CreateConnection() =>
        new(_config.GetConnectionString("DefaultConnection"));

    /// <summary>
    /// Upload a receipt image for OCR processing
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadReceipt(IFormFile file)
    {
        try
        {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        if (file.Length > 10 * 1024 * 1024) // 10MB max
            return BadRequest(new { message = "File too large (max 10MB)" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
        
        // Fallback: derive extension from content type if filename has none
        if (string.IsNullOrEmpty(extension))
        {
            extension = file.ContentType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { message = "Invalid file type. Use JPG, PNG, GIF, or WebP." });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Save file — use AppContext.BaseDirectory which works reliably on IIS
        var basePath = AppContext.BaseDirectory;
        var uploadsPath = _config["Uploads:ReceiptsPath"];
        if (string.IsNullOrEmpty(uploadsPath))
            uploadsPath = Path.Combine(basePath, "wwwroot", "uploads", "receipts");
        _logger.LogInformation("Uploads path: {Path}, BaseDir: {Base}", uploadsPath, basePath);
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Extract receipt data with OpenAI Vision
        var receiptData = await _receiptService.ExtractReceiptDataAsync(filePath);

        // Store meal record
        var relativePath = $"/uploads/receipts/{fileName}";
        using var conn = CreateConnection();
        var mealId = await conn.QuerySingleAsync<int>(
            @"INSERT INTO Meals (UserId, PhotoPath, ExtractedTotal, ExtractedDate, ExtractedRestaurant, Status, CreatedAt)
              OUTPUT INSERTED.Id
              VALUES (@UserId, @PhotoPath, @ExtractedTotal, @ExtractedDate, @ExtractedRestaurant, @Status, GETUTCDATE())",
            new
            {
                UserId = userId,
                PhotoPath = relativePath,
                ExtractedTotal = receiptData.Total,
                ExtractedDate = receiptData.Date,
                ExtractedRestaurant = receiptData.Restaurant,
                Status = receiptData.Confident ? "Verified" : "Pending"
            });

        // If confident, check for reward eligibility
        if (receiptData.Confident)
        {
            await CheckAndAwardReward(conn, userId);
        }

        return Ok(new UploadReceiptResponse
        {
            MealId = mealId,
            ExtractedTotal = receiptData.Total,
            ExtractedDate = receiptData.Date,
            ExtractedRestaurant = receiptData.Restaurant,
            NeedsManualEntry = !receiptData.Confident
        });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload receipt failed");
            return StatusCode(500, new { message = "Upload failed: " + ex.Message, stack = ex.StackTrace, inner = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Confirm a pending meal (with optional manual total)
    /// </summary>
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmMeal(int id, [FromBody] ConfirmMealRequest request)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var conn = CreateConnection();

        var meal = await conn.QueryFirstOrDefaultAsync<Meal>(
            "SELECT * FROM Meals WHERE Id = @Id AND UserId = @UserId",
            new { Id = id, UserId = userId });

        if (meal == null)
            return NotFound(new { message = "Meal not found" });

        if (meal.Status == "Verified")
            return BadRequest(new { message = "Meal already verified" });

        await conn.ExecuteAsync(
            @"UPDATE Meals SET 
                ManualTotal = @ManualTotal, 
                Status = 'Verified',
                CreatedAt = CASE WHEN CreatedAt IS NULL THEN GETUTCDATE() ELSE CreatedAt END
              WHERE Id = @Id",
            new { ManualTotal = request.ManualTotal, Id = id });

        // Check for reward eligibility
        await CheckAndAwardReward(conn, userId);

        return Ok(new { message = "Meal confirmed" });
    }

    /// <summary>
    /// Get user's meals (paginated)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMeals([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var conn = CreateConnection();

        var offset = (page - 1) * pageSize;
        var meals = await conn.QueryAsync<MealDto>(
            @"SELECT Id, UserId, PhotoPath, ExtractedTotal, ExtractedDate, ExtractedRestaurant, ManualTotal, Status, CreatedAt
              FROM Meals WHERE UserId = @UserId
              ORDER BY CreatedAt DESC
              OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { UserId = userId, Offset = offset, PageSize = pageSize });

        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Meals WHERE UserId = @UserId",
            new { UserId = userId });

        return Ok(new { meals, total, page, pageSize });
    }

    /// <summary>
    /// Get customer dashboard progress
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var conn = CreateConnection();

        // Rolling 3-month window
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddMonths(-3);

        var mealsInPeriod = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Meals 
              WHERE UserId = @UserId AND Status = 'Verified' AND CreatedAt >= @PeriodStart",
            new { UserId = userId, PeriodStart = periodStart });

        var recentMeals = await conn.QueryAsync<MealDto>(
            @"SELECT TOP 5 Id, UserId, PhotoPath, ExtractedTotal, ExtractedDate, ExtractedRestaurant, ManualTotal, Status, CreatedAt
              FROM Meals WHERE UserId = @UserId AND Status = 'Verified'
              ORDER BY CreatedAt DESC",
            new { UserId = userId });

        var activeRewards = await conn.QueryAsync<RewardDto>(
            @"SELECT r.Id, r.UserId, r.Type, r.Status, r.EarnedAt, r.RedeemedAt, r.PeriodStart, r.PeriodEnd
              FROM Rewards r
              WHERE r.UserId = @UserId AND r.Status = 'Earned'
              ORDER BY r.EarnedAt DESC",
            new { UserId = userId });

        var notifications = await conn.QueryAsync<Notification>(
            @"SELECT TOP 10 Id, UserId, Message, IsRead, CreatedAt
              FROM Notifications WHERE UserId = @UserId
              ORDER BY CreatedAt DESC",
            new { UserId = userId });

        return Ok(new DashboardProgress
        {
            MealsInPeriod = mealsInPeriod,
            MealsRequired = 10,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RecentMeals = recentMeals.ToList(),
            ActiveRewards = activeRewards.ToList(),
            Notifications = notifications.ToList()
        });
    }

    /// <summary>
    /// Check if user qualifies for a reward and award it
    /// </summary>
    private async Task CheckAndAwardReward(SqlConnection conn, int userId)
    {
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddMonths(-3);

        var verifiedMeals = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM Meals 
              WHERE UserId = @UserId AND Status = 'Verified' AND CreatedAt >= @PeriodStart",
            new { UserId = userId, PeriodStart = periodStart });

        // Check if they've hit 10 meals and don't already have an earned reward for this period
        if (verifiedMeals >= 10)
        {
            var existingReward = await conn.QueryFirstOrDefaultAsync<Reward>(
                @"SELECT TOP 1 * FROM Rewards 
                  WHERE UserId = @UserId AND Status = 'Earned' AND PeriodEnd >= @PeriodStart
                  ORDER BY EarnedAt DESC",
                new { UserId = userId, PeriodStart = periodStart });

            // Only award if the last reward was redeemed or there's no reward yet
            // Check multiples of 10 to allow multiple rewards
            var earnedRewardsInPeriod = await conn.ExecuteScalarAsync<int>(
                @"SELECT COUNT(*) FROM Rewards 
                  WHERE UserId = @UserId AND PeriodEnd >= @PeriodStart",
                new { UserId = userId, PeriodStart = periodStart });

            var rewardsDeserved = verifiedMeals / 10;
            if (earnedRewardsInPeriod < rewardsDeserved)
            {
                // Award new reward
                await conn.ExecuteAsync(
                    @"INSERT INTO Rewards (UserId, Type, Status, EarnedAt, PeriodStart, PeriodEnd)
                      VALUES (@UserId, 'FreeMeal', 'Earned', GETUTCDATE(), @PeriodStart, @PeriodEnd)",
                    new { UserId = userId, PeriodStart = periodStart, PeriodEnd = periodEnd });

                // Create notification
                await conn.ExecuteAsync(
                    @"INSERT INTO Notifications (UserId, Message, IsRead, CreatedAt)
                      VALUES (@UserId, @Message, 0, GETUTCDATE())",
                    new
                    {
                        UserId = userId,
                        Message = "🎉 Congratulations! You've completed 10 meals and earned a FREE meal! Show this at the restaurant to redeem."
                    });

                // Send congratulatory SMS
                var user = await conn.QueryFirstOrDefaultAsync<User>(
                    "SELECT * FROM Users WHERE Id = @Id", new { Id = userId });

                if (user != null)
                {
                    await _smsService.SendSmsAsync(user.Phone,
                        "Congratulations! You've completed 10 meals in 3 months and qualify for a FREE meal! Show this message at the restaurant to redeem. - USushi");
                }

                _logger.LogInformation("Reward earned by user {UserId} - {VerifiedMeals} meals in period", userId, verifiedMeals);
            }
        }
    }
}
