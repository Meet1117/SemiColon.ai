using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models;

public class AppUser
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Null for users who signed up with Google only.
    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public bool IsEmailVerified { get; set; }

    // Profile photo: an uploaded file path or the Google account picture URL.
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    // While this is fresh (2h window), password login skips the email OTP.
    public DateTime? LastOtpVerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
}
