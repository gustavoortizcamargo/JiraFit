namespace JiraFit.Domain.Entities;

public class MealAlarm
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    
    public string Name { get; private set; }
    public int Hour { get; private set; } // 0-23
    public int Minute { get; private set; } // 0-59
    
    public bool IsActive { get; private set; }
    public DateTime? LastTriggeredAt { get; private set; }

    // Navigation
    public User? User { get; private set; }

    protected MealAlarm() { } // EF Core

    public MealAlarm(Guid userId, string name, int hour, int minute)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Name = name;
        Hour = hour;
        Minute = minute;
        IsActive = true;
    }

    public void MarkAsTriggered(DateTime timestamp)
    {
        LastTriggeredAt = timestamp;
    }

    public void Toggle(bool isActive)
    {
        IsActive = isActive;
    }
}
