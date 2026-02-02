using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace ProjectTemplate.Api.Services;

public class SmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmsService> _logger;
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _fromNumber;

    public SmsService(IConfiguration config, ILogger<SmsService> logger)
    {
        _config = config;
        _logger = logger;

        _accountSid = _config["Twilio:AccountSid"] ?? throw new InvalidOperationException("Twilio:AccountSid not configured");
        _authToken = _config["Twilio:AuthToken"] ?? throw new InvalidOperationException("Twilio:AuthToken not configured");
        _fromNumber = _config["Twilio:FromNumber"] ?? throw new InvalidOperationException("Twilio:FromNumber not configured");

        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task<bool> SendSmsAsync(string toPhone, string message)
    {
        try
        {
            // Ensure E.164 format for US numbers
            var to = toPhone.StartsWith("+") ? toPhone : $"+1{toPhone}";
            var from = _fromNumber.StartsWith("+") ? _fromNumber : $"+{_fromNumber}";

            var result = await MessageResource.CreateAsync(
                to: new PhoneNumber(to),
                from: new PhoneNumber(from),
                body: message
            );

            _logger.LogInformation("SMS sent to {Phone}, SID: {Sid}, Status: {Status}", toPhone, result.Sid, result.Status);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twilio SMS send error to {Phone}", toPhone);
            return false;
        }
    }

    public string GenerateOtpCode()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }
}
