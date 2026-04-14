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

    // SMS Verification
    public bool IsVerified { get; private set; }
    public string? VerificationCode { get; private set; }
    public DateTime? VerificationExpiresAt { get; private set; }

    protected DashboardUser() { } // EF Core

    public DashboardUser(string email, string passwordHash, string phoneNumber, string role = "User")
    {
        Id = Guid.NewGuid();
        Email = email;
        PasswordHash = passwordHash;
        PhoneNumber = phoneNumber;
        Role = role;
        CreatedAt = DateTime.UtcNow.AddHours(-3);
        IsVerified = false;
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

    public void SetVerificationCode(string code, int expirationMinutes = 10)
    {
        VerificationCode = code;
        VerificationExpiresAt = DateTime.UtcNow.AddHours(-3).AddMinutes(expirationMinutes);
    }

    public bool ValidateVerificationCode(string code)
    {
        if (IsVerified) return true;
        if (VerificationCode == null || VerificationExpiresAt == null) return false;
        if (DateTime.UtcNow.AddHours(-3) > VerificationExpiresAt.Value) return false;
        if (VerificationCode != code) return false;

        IsVerified = true;
        VerificationCode = null;
        VerificationExpiresAt = null;
        return true;
    }
}
