using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models;

public class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }
}
