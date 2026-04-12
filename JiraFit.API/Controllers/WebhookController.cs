using JiraFit.Application.DTOs;
using JiraFit.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JiraFit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WebhookController : ControllerBase
{
    private readonly IWebhookProcessorService _processorService;

    public WebhookController(IWebhookProcessorService processorService)
    {
        _processorService = processorService;
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Receive([FromForm] IFormCollection form)
    {
        // Extraction of Twilio parameters
        var fromNumber = form["From"].ToString();
        var bodyMsg = form["Body"].ToString();
        var numMediaStr = form["NumMedia"].ToString();
        
        string? mediaUrl = null;
        string? mediaType = null;

        if (int.TryParse(numMediaStr, out int numMedia) && numMedia > 0)
        {
            mediaUrl = form["MediaUrl0"].ToString();
            mediaType = form["MediaContentType0"].ToString();
        }

        var dto = new MealInputDto
        {
            UserPhoneNumber = fromNumber,
            TextContent = bodyMsg,
            MediaUrl = mediaUrl,
            MediaType = mediaType
        };

        // Enqueue the request for background processing
        await _processorService.EnqueueWebhookPayloadAsync(dto);

        // Always return 202 immediately to Twilio so it doesnt timeout.
        return Accepted();
    }
}
