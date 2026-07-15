using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models;

public enum OtpPurpose
{
    Signup,
    Login
}

public class OtpCode
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(6)]
    public string Code { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int FailedAttempts { get; set; }
}
