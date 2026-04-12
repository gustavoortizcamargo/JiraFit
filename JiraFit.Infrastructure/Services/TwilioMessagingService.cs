using System.Net.Http.Headers;
using JiraFit.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JiraFit.Infrastructure.Services;

public class TwilioMessagingService : IMessagingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioMessagingService> _logger;

    public TwilioMessagingService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TwilioMessagingService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var accountSid = _configuration["Twilio:AccountSid"];
            var authToken = _configuration["Twilio:AuthToken"];
            
            // This is the number you bought or your Sandbox number
            // E.g.: "whatsapp:+14155238886"
            var fromNumber = _configuration["Twilio:FromNumber"]; 

            if (string.IsNullOrEmpty(accountSid) || string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(fromNumber))
            {
                _logger.LogWarning("Twilio credentials (AccountSid, AuthToken or FromNumber) are missing. Skipping real send.");
                // Fallback to console if missing config for local testing
                Console.WriteLine($"[TWILIO MOCK] Sending to {toPhoneNumber}: {message}");
                return;
            }

            var twilioClient = _httpClientFactory.CreateClient("Twilio");
            
            var authHeaderValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}"));
            twilioClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

            var requestData = new Dictionary<string, string>
            {
                { "To", toPhoneNumber }, // Should already have "whatsapp:" prefix
                { "From", fromNumber },
                { "Body", message }
            };

            var content = new FormUrlEncodedContent(requestData);
            
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var response = await twilioClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to send Twilio WhatsApp Message. Status: {Status}, Content: {Content}", response.StatusCode, errorResponse);
            }
            else
            {
                _logger.LogInformation("WhatsApp message successfully sent to {To}", toPhoneNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while trying to send message to {To}", toPhoneNumber);
        }
    }
}
