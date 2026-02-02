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
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly SmsService _smsService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IConfiguration config, SmsService smsService, ILogger<AdminController> logger)
    {
        _config = config;
        _smsService = smsService;
        _logger = logger;
    }

    private SqlConnection CreateConnection() =>
        new(_config.GetConnectionString("DefaultConnection"));

    /// <summary>
    /// Get admin dashboard stats
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        using var conn = CreateConnection();

        var totalUsers = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Role = 'User'");
        var mealsToday = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Meals WHERE CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE)");
        var activeRewards = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Rewards WHERE Status = 'Earned'");
        var pendingRewards = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Rewards WHERE Status = 'Earned'");

        var recentMeals = await conn.QueryAsync<MealDto>(
            @"SELECT TOP 10 m.Id, m.UserId, m.PhotoPath, m.ExtractedTotal, m.ExtractedDate, 
                     m.ExtractedRestaurant, m.ManualTotal, m.Status, m.CreatedAt
              FROM Meals m
              ORDER BY m.CreatedAt DESC");

        return Ok(new AdminDashboardStats
        {
            TotalUsers = totalUsers,
            MealsToday = mealsToday,
            ActiveRewards = activeRewards,
            PendingRewards = pendingRewards,
            RecentMeals = recentMeals.ToList()
        });
    }

    /// <summary>
    /// Get all users with stats
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        using var conn = CreateConnection();

        var users = await conn.QueryAsync<UserWithStats>(
            @"SELECT u.Id, u.Phone, u.DisplayName, u.Role, u.IsActive, u.CreatedAt,
                     ISNULL(m.MealCount, 0) AS MealCount,
                     m.LastMealAt
              FROM Users u
              LEFT JOIN (
                  SELECT UserId, COUNT(*) AS MealCount, MAX(CreatedAt) AS LastMealAt
                  FROM Meals WHERE Status = 'Verified'
                  GROUP BY UserId
              ) m ON m.UserId = u.Id
              ORDER BY u.CreatedAt DESC");

        return Ok(users);
    }

    /// <summary>
    /// Update a user (admin)
    /// </summary>
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        using var conn = CreateConnection();

        var user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id", new { Id = id });

        if (user == null) return NotFound(new { message = "User not found" });

        if (request.DisplayName != null)
            user.DisplayName = request.DisplayName;
        if (request.Role != null)
            user.Role = request.Role;
        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        await conn.ExecuteAsync(
            @"UPDATE Users SET
                DisplayName = @DisplayName,
                Role = @Role,
                IsActive = @IsActive,
                UpdatedAt = GETUTCDATE()
              WHERE Id = @Id",
            new { user.DisplayName, user.Role, user.IsActive, Id = id });

        _logger.LogInformation("User {Id} updated by admin {Admin}", id, User.Identity?.Name);
        return Ok(new { message = "User updated" });
    }

    /// <summary>
    /// Send SMS broadcast to all (or active) users
    /// </summary>
    [HttpPost("sms-broadcast")]
    public async Task<IActionResult> SendSmsBroadcast([FromBody] SmsBroadcastRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message is required" });

        if (request.Message.Length > 160)
            return BadRequest(new { message = "Message too long (max 160 characters)" });

        var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        using var conn = CreateConnection();

        // Get recipients
        var sql = "SELECT Phone FROM Users WHERE Role = 'User' AND IsActive = 1";
        if (request.ActiveOnly)
        {
            sql = @"SELECT DISTINCT u.Phone FROM Users u
                    INNER JOIN Meals m ON m.UserId = u.Id
                    WHERE u.Role = 'User' AND u.IsActive = 1
                    AND m.CreatedAt >= DATEADD(MONTH, -3, GETUTCDATE())";
        }

        var phones = (await conn.QueryAsync<string>(sql)).ToList();

        if (phones.Count == 0)
            return BadRequest(new { message = "No recipients found" });

        // Send SMS to each user
        var successCount = 0;
        foreach (var phone in phones)
        {
            var sent = await _smsService.SendSmsAsync(phone, request.Message);
            if (sent) successCount++;
        }

        // Log broadcast
        await conn.ExecuteAsync(
            @"INSERT INTO SmsBroadcasts (AdminUserId, Message, RecipientCount, SentAt)
              VALUES (@AdminUserId, @Message, @RecipientCount, GETUTCDATE())",
            new { AdminUserId = adminUserId, request.Message, RecipientCount = successCount });

        _logger.LogInformation("SMS broadcast sent by admin {AdminId}: {Count} recipients", adminUserId, successCount);
        return Ok(new { message = $"SMS sent to {successCount}/{phones.Count} users", recipientCount = successCount });
    }

    /// <summary>
    /// Get SMS broadcast history
    /// </summary>
    [HttpGet("sms-broadcasts")]
    public async Task<IActionResult> GetSmsBroadcasts()
    {
        using var conn = CreateConnection();

        var broadcasts = await conn.QueryAsync<SmsBroadcastDto>(
            @"SELECT Id, AdminUserId, Message, RecipientCount, SentAt
              FROM SmsBroadcasts
              ORDER BY SentAt DESC");

        return Ok(broadcasts);
    }
}
