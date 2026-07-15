using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models;

public class ChatSession
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [MaxLength(120)]
    public string Title { get; set; } = "New chat";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
