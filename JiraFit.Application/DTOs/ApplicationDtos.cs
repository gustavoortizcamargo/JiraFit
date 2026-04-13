namespace JiraFit.Application.DTOs;

public class MealInputDto
{
    public string UserPhoneNumber { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; } // e.g. "image/jpeg" or "audio/ogg"
    public string? TextContent { get; set; }
    public string? ContextMetadata { get; set; }
}

public class NutritionalAnalysisDto
{
    public double Calories { get; set; }
    public double Proteins { get; set; }
    public double Carbs { get; set; }
    public double Fats { get; set; }
    public string Feedback { get; set; } = string.Empty;
    
    // Extracted context for Onboarding/Update
    public string? ExtractedName { get; set; }
    public double? ExtractedWeight { get; set; }
    public double? ExtractedHeight { get; set; }

    // Extracted context for Meal Alarms
    public string? AlarmName { get; set; }
    public int? AlarmHour { get; set; }
    public int? AlarmMinute { get; set; }
}

public class UserRegistrationDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Height { get; set; }
    public int Age { get; set; }
    public string Gender { get; set; } = string.Empty; // "Male" / "Female"
    public string Objective { get; set; } = string.Empty; // "WeightLoss", etc.
    public bool IsChildishPalate { get; set; }
}
