namespace JiraFit.Domain.Entities;

public class DashboardUser
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    
    // Link to WhatsApp User by phone number
    public string PhoneNumber { get; private set; }
    public Guid? LinkedUserId { get; private set; }
    
    public string Role { get; private set; } // "Admin" or "User"
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    protected DashboardUser() { } // EF Core

    public DashboardUser(string email, string passwordHash, string phoneNumber, string role = "User")
    {
        Id = Guid.NewGuid();
        Email = email;
        PasswordHash = passwordHash;
        PhoneNumber = phoneNumber;
        Role = role;
        CreatedAt = DateTime.UtcNow.AddHours(-3);
    }

    public void LinkToWhatsAppUser(Guid userId)
    {
        LinkedUserId = userId;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow.AddHours(-3);
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
    }
}
