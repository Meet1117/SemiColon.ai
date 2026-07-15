using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models;

public class ChatMessage
{
    public int Id { get; set; }

    public int ChatSessionId { get; set; }
    public ChatSession ChatSession { get; set; } = null!;

    // "user" or "model" — matches Gemini role names.
    [MaxLength(10)]
    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
