using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using System.Text;

namespace JiraFit.API.Middlewares;

public class TwilioSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _authToken; // Twilio Auth Token from Configuration

    public TwilioSignatureMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _authToken = configuration["Twilio:AuthToken"] ?? string.Empty;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/webhook") && context.Request.Method == "POST")
        {
            var twilioSignature = context.Request.Headers["X-Twilio-Signature"].ToString();
            
            // For now, if the token is not configured or signature is empty, we might bypass for local dev
            // In a real scenario we throw returning 401 Unauthorized.
            if (string.IsNullOrEmpty(_authToken) && context.Request.Host.Host == "localhost")
            {
                await _next(context);
                return;
            }

            // Implement specific Twilio webhook validation logic here
            // Example structure:
            // 1. Get full URL including scheme/host/path
            // 2. Read request form parameters, sort them
            // 3. Append to URL
            // 4. HMAC-SHA1 using _authToken
            // 5. Compare with twilioSignature

            // NOTE: Since this requires reading the Form, which we might need in Controller, 
            // EnableBuffering is necessary if we read body stream as text, but for form is accessible.

            // Mock validation logic
            bool isValid = !string.IsNullOrEmpty(twilioSignature) || context.Request.Host.Host == "localhost";
            
            if (!isValid)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid signature");
                return;
            }
        }

        await _next(context);
    }
}
