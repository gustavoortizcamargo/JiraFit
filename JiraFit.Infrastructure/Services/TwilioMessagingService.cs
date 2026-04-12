using JiraFit.Application.Interfaces;

namespace JiraFit.Infrastructure.Services;

public class TwilioMessagingService : IMessagingService
{
    public async Task SendMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // TODO: Use Twilio SDK or HttpClient to send the outcome back to the user
        // Example: await TwilioClient.SendMessage(...)
        
        Console.WriteLine($"[TWILIO MOCK] Sending to {toPhoneNumber}: {message}");
        
        await Task.CompletedTask;
    }
}
