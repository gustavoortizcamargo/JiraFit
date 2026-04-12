namespace JiraFit.Domain.Entities;

public class Meal
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    
    // Either ImageUrl or RawText or both might be present
    public string? ImageUrl { get; private set; }
    public string? RawText { get; private set; }
    
    // Nutritional Info from AI
    public double Calories { get; private set; }
    public double Proteins { get; private set; }
    public double Carbs { get; private set; }
    public double Fats { get; private set; }
    public string? AiFeedback { get; private set; }
    
    public DateTime Timestamp { get; private set; }

    // Navigation
    public User? User { get; private set; }

    protected Meal() { } // EF Core

    public Meal(Guid userId, string? imageUrl, string? rawText, double calories, double proteins, double carbs, double fats, string? aiFeedback)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ImageUrl = imageUrl;
        RawText = rawText;
        Calories = calories;
        Proteins = proteins;
        Carbs = carbs;
        Fats = fats;
        AiFeedback = aiFeedback;
        Timestamp = DateTime.UtcNow;
    }
}
