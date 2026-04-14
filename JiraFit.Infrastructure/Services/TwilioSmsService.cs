using System.Net.Http.Headers;
using JiraFit.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JiraFit.Infrastructure.Services;

public class TwilioSmsService : ISmsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioSmsService> _logger;

    public TwilioSmsService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TwilioSmsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendSmsAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            // Use the SMS-capable number (not the WhatsApp sandbox one)
            var fromNumber = _configuration["Twilio:SmsFromNumber"] ?? _configuration["Twilio:FromNumber"];

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(fromNumber))
            {
                _logger.LogWarning("Twilio SMS credentials missing. Falling back to console.");
                Console.WriteLine($"[SMS MOCK] To: {toPhoneNumber} | Message: {message}");
                return;
            }

            var client = _httpClientFactory.CreateClient("Twilio");
            var authHeaderValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            // Strip "whatsapp:" prefix if present — SMS uses raw phone numbers
            var smsTo = toPhoneNumber.Replace("whatsapp:", "");

            var requestData = new Dictionary<string, string>
            {
                { "To", smsTo },
                { "From", fromNumber.Replace("whatsapp:", "") },
                { "Body", message }
            };

            var content = new FormUrlEncodedContent(requestData);
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var response = await client.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Twilio SMS failed. Status: {Status}, Body: {Body}", response.StatusCode, errorBody);
            }
            else
            {
                _logger.LogInformation("SMS sent successfully to {To}", smsTo);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {To}", toPhoneNumber);
        }
    }
}
