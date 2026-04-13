using JiraFit.Domain.Enums;

namespace JiraFit.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string PhoneNumber { get; private set; } // Identifier for Whatsapp
    public string? Name { get; private set; }
    public double Weight { get; private set; } // kg
    public double Height { get; private set; } // cm
    public int Age { get; private set; }
    public Gender Gender { get; private set; }
    public Objective Objective { get; private set; }
    
    // Calculated values
    public double Bmr { get; private set; } // Taxa Metabólica Basal
    public double Tdee { get; private set; } // Valor Energético Total
    
    public bool IsChildishPalate { get; private set; }

    // Gamification
    public int CurrentStreak { get; private set; }
    public DateTime? LastActivityDate { get; private set; }

    // Subscription & Paywall
    public DateTime CreatedAt { get; private set; }
    public bool IsPro { get; private set; }
    public int MessagesSentToday { get; private set; }
    public DateTime? LastMessageTrackedDate { get; private set; }

    public void TrackMessageUsage(DateTime currentLocalDate)
    {
        var targetDate = currentLocalDate.Date;

        if (LastMessageTrackedDate == null || LastMessageTrackedDate.Value.Date != targetDate)
        {
            MessagesSentToday = 1; // First message of the day
        }
        else
        {
            MessagesSentToday++;
        }

        LastMessageTrackedDate = targetDate;
    }

    public void UpgradeToPro()
    {
        IsPro = true;
    }

    public void RegisterActivity(DateTime currentLocalDate)
    {
        var targetDate = currentLocalDate.Date;

        if (LastActivityDate == null)
        {
            CurrentStreak = 1;
        }
        else
        {
            var diff = (targetDate - LastActivityDate.Value.Date).TotalDays;

            if (diff == 1) // Consecutive Day
            {
                CurrentStreak++;
            }
            else if (diff > 1) // Gap > 1 day, Streak broken
            {
                CurrentStreak = 1;
            }
            // diff == 0 means another meal today, ignore
        }

        LastActivityDate = targetDate;
    }

    // Navigation
    private readonly List<Meal> _meals = new();
    public IReadOnlyCollection<Meal> Meals => _meals.AsReadOnly();
    
    private readonly List<MealAlarm> _alarms = new();
    public IReadOnlyCollection<MealAlarm> Alarms => _alarms.AsReadOnly();

    protected User() { } // For EF Core

    // Only Phone Number initially provided
    public User(string phoneNumber)
    {
        Id = Guid.NewGuid();
        PhoneNumber = phoneNumber;
        Objective = Objective.Maintenance; // Default logic
        CreatedAt = DateTime.UtcNow; // UTC for global control
        IsPro = false; // Start on Free Tier
    }

    public void UpdateProfile(string? name, double? weight, double? height)
    {
        if (!string.IsNullOrEmpty(name)) Name = name;
        if (weight.HasValue && weight > 0) Weight = weight.Value;
        if (height.HasValue && height > 0) Height = height.Value;

        if (Weight > 0 && Height > 0)
        {
            if (Age == 0) Age = 30; // Default fallback for formula
            CalculateMetabolicRates();
        }
    }

    private void CalculateMetabolicRates()
    {
        // Harris-Benedict Formula
        if (Gender == Gender.Male)
        {
            Bmr = 88.362 + (13.397 * Weight) + (4.799 * Height) - (5.677 * Age);
        }
        else
        {
            Bmr = 447.593 + (9.247 * Weight) + (3.098 * Height) - (4.330 * Age);
        }

        double activityMultiplier = 1.2; 
        double tdeeBase = Bmr * activityMultiplier;

        Tdee = Objective switch
        {
            Objective.WeightLoss => tdeeBase - 500, // Deficit
            Objective.MuscleGain => tdeeBase + 300, // Surplus
            _ => tdeeBase // Maintenance
        };
    }
}
