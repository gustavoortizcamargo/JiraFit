namespace JiraFit.Application.DTOs;

public class MealInputDto
{
    public string UserPhoneNumber { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? TextContent { get; set; }
    public string? AudioContentMode { get; set; } // If Twilio provides audio transcript or generic
}

public class NutritionalAnalysisDto
{
    public double Calories { get; set; }
    public double Proteins { get; set; }
    public double Carbs { get; set; }
    public double Fats { get; set; }
    public string Feedback { get; set; } = string.Empty;
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
