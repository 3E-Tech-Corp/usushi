using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminController(IConfiguration config, SmsService smsService, ILogger<AdminController> logger, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _smsService = smsService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
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
            @"SELECT u.Id, u.Phone, u.DisplayName, u.Role, u.IsActive, u.IsPhoneVerified, u.CreatedAt,
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

    /// <summary>
    /// Scan an image of handwritten phone numbers using OpenAI Vision
    /// </summary>
    [HttpPost("scan-phones")]
    public async Task<IActionResult> ScanPhones(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest(new { message = "File too large (max 10MB)" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";

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

            // Save file
            var basePath = AppContext.BaseDirectory;
            var uploadsPath = Path.Combine(basePath, "wwwroot", "uploads", "scan-phones");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"scan_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsPath, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Send to OpenAI Vision
            var apiKey = _config["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "__OPENAI_API_KEY__")
            {
                return BadRequest(new { message = "OpenAI API key not configured" });
            }

            var imageBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var base64Image = Convert.ToBase64String(imageBytes);
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            var requestBody = new
            {
                model = "gpt-4o-mini",
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = @"Analyze this image of handwritten phone numbers. Extract ALL phone numbers you can read.
For each number, also extract any name written next to it if visible.
Respond ONLY with a JSON object (no markdown, no code blocks):
{
  ""phones"": [
    {""phone"": ""1234567890"", ""name"": ""John Doe""},
    {""phone"": ""9876543210"", ""name"": null}
  ]
}
Normalize all phone numbers to 10 digits (US format, no country code, no dashes/spaces/parentheses).
If you cannot read a number clearly, include your best guess but add ""uncertain"": true.
If the image doesn't contain phone numbers, return {""phones"": []}."
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:{mimeType};base64,{base64Image}",
                                    detail = "low"
                                }
                            }
                        }
                    }
                },
                max_tokens = 2000
            };

            var httpClient = _httpClientFactory.CreateClient("OpenAI");
            var json = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI API error during phone scan: {Status} {Body}", response.StatusCode, responseBody);
                return StatusCode(502, new { message = "Failed to process image with AI" });
            }

            // Parse the OpenAI response
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrEmpty(content))
                return Ok(new ScanPhonesResponse());

            // Clean content - remove markdown code blocks if present
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewline = content.IndexOf('\n');
                if (firstNewline >= 0)
                    content = content[(firstNewline + 1)..];
                if (content.EndsWith("```"))
                    content = content[..^3];
                content = content.Trim();
            }

            using var resultDoc = JsonDocument.Parse(content);
            var root = resultDoc.RootElement;

            var scannedPhones = new List<ScannedPhone>();
            if (root.TryGetProperty("phones", out var phonesArray))
            {
                // Check existing phones in DB
                using var conn = CreateConnection();
                var existingPhones = (await conn.QueryAsync<string>("SELECT Phone FROM Users")).ToHashSet();

                foreach (var phoneEl in phonesArray.EnumerateArray())
                {
                    var phone = phoneEl.GetProperty("phone").GetString() ?? "";
                    string? name = null;
                    if (phoneEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                        name = nameEl.GetString();
                    bool uncertain = false;
                    if (phoneEl.TryGetProperty("uncertain", out var uncertainEl))
                        uncertain = uncertainEl.ValueKind == JsonValueKind.True;

                    scannedPhones.Add(new ScannedPhone
                    {
                        Phone = phone,
                        Name = name,
                        AlreadyExists = existingPhones.Contains(phone),
                        Uncertain = uncertain
                    });
                }
            }

            _logger.LogInformation("Phone scan completed: {Count} numbers found", scannedPhones.Count);
            return Ok(new ScanPhonesResponse { Phones = scannedPhones });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning phone numbers from image");
            return StatusCode(500, new { message = "Failed to scan phone numbers: " + ex.Message });
        }
    }

    /// <summary>
    /// Import confirmed phone numbers as unverified users
    /// </summary>
    [HttpPost("import-phones")]
    public async Task<IActionResult> ImportPhones([FromBody] ImportPhonesRequest request)
    {
        if (request.Phones == null || request.Phones.Count == 0)
            return BadRequest(new { message = "No phone numbers provided" });

        using var conn = CreateConnection();
        var importedPhones = new List<string>();
        var skippedCount = 0;

        foreach (var phone in request.Phones)
        {
            // Check if user already exists
            var existing = await conn.QueryFirstOrDefaultAsync<User>(
                "SELECT Id FROM Users WHERE Phone = @Phone",
                new { Phone = phone });

            if (existing != null)
            {
                skippedCount++;
                continue;
            }

            // Create unverified user
            await conn.ExecuteAsync(
                @"INSERT INTO Users (Phone, Role, IsActive, IsPhoneVerified, CreatedAt)
                  VALUES (@Phone, 'User', 1, 0, GETUTCDATE())",
                new { Phone = phone });

            importedPhones.Add(phone);
        }

        _logger.LogInformation("Phone import: {Imported} imported, {Skipped} skipped", importedPhones.Count, skippedCount);

        return Ok(new ImportPhonesResponse
        {
            ImportedCount = importedPhones.Count,
            SkippedCount = skippedCount,
            ImportedPhones = importedPhones
        });
    }

    /// <summary>
    /// Send a test SMS to a specific user
    /// </summary>
    [HttpPost("test-sms/{userId}")]
    public async Task<IActionResult> SendTestSms(int userId, [FromBody] TestSmsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message is required" });

        if (request.Message.Length > 160)
            return BadRequest(new { message = "Message too long (max 160 characters)" });

        using var conn = CreateConnection();
        var user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id",
            new { Id = userId });

        if (user == null)
            return NotFound(new { message = "User not found" });

        var sent = await _smsService.SendSmsAsync(user.Phone, request.Message);

        if (sent)
        {
            _logger.LogInformation("Test SMS sent to user {UserId} ({Phone})", userId, user.Phone);
            return Ok(new { message = $"SMS sent to {user.Phone}" });
        }

        return StatusCode(502, new { message = "Failed to send SMS" });
    }
}
