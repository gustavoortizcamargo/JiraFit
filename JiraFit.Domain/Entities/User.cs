using JiraFit.Domain.Enums;

namespace JiraFit.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string PhoneNumber { get; private set; } // Identifier for Whatsapp
    public string Name { get; private set; }
    public double Weight { get; private set; } // kg
    public double Height { get; private set; } // cm
    public int Age { get; private set; }
    public Gender Gender { get; private set; }
    public Objective Objective { get; private set; }
    
    // Calculated values
    public double Bmr { get; private set; } // Taxa Metabólica Basal
    public double Tdee { get; private set; } // Valor Energético Total
    
    public bool IsChildishPalate { get; private set; }

    // Navigation
    private readonly List<Meal> _meals = new();
    public IReadOnlyCollection<Meal> Meals => _meals.AsReadOnly();

    protected User() { } // For EF Core

    public User(string phoneNumber, string name, double weight, double height, int age, Gender gender, Objective objective, bool isChildishPalate = false)
    {
        Id = Guid.NewGuid();
        PhoneNumber = phoneNumber;
        Name = name;
        Weight = weight;
        Height = height;
        Age = age;
        Gender = gender;
        Objective = objective;
        IsChildishPalate = isChildishPalate;

        CalculateMetabolicRates();
    }

    public void UpdateProfile(double weight, double height, int age, Objective objective, bool isChildishPalate)
    {
        Weight = weight;
        Height = height;
        Age = age;
        Objective = objective;
        IsChildishPalate = isChildishPalate;

        CalculateMetabolicRates();
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

        // Default sedentary multiplier for VET (can be improved later with activity levels)
        double activityMultiplier = 1.2; 
        
        // Adjust based on Objective
        double tdeeBase = Bmr * activityMultiplier;

        Tdee = Objective switch
        {
            Objective.WeightLoss => tdeeBase - 500, // Deficit
            Objective.MuscleGain => tdeeBase + 300, // Surplus
            _ => tdeeBase // Maintenance
        };
    }
}
