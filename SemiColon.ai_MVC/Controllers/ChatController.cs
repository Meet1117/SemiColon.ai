using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SemiColon.ai_MVC.Data;
using SemiColon.ai_MVC.Models;
using SemiColon.ai_MVC.Services;

namespace SemiColon.ai_MVC.Controllers;

[Authorize]
public class ChatController : Controller
{
    private readonly AppDbContext _db;
    private readonly GeminiService _gemini;
    private readonly IConfiguration _config;

    public ChatController(AppDbContext db, GeminiService gemini, IConfiguration config)
    {
        _db = db;
        _gemini = gemini;
        _config = config;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sessions = await _db.ChatSessions
            .Where(s => s.UserId == UserId)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        ViewBag.CurrentUser = await _db.Users.FirstAsync(u => u.Id == UserId);
        return View(sessions);
    }

    [HttpGet]
    public async Task<IActionResult> Messages(int sessionId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.ChatSessionId == sessionId && m.ChatSession.UserId == UserId)
            .OrderBy(m => m.Id)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();

        return Json(messages);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting("chat")]
    public async Task<IActionResult> SendMessage(int? sessionId, string message)
    {
        message = (message ?? string.Empty).Trim();
        int maxLength = _config.GetValue<int>("RateLimit:MaxMessageLength", 4000);

        if (message.Length == 0)
            return BadRequest(new { error = "Message cannot be empty." });
        if (message.Length > maxLength)
            return BadRequest(new { error = $"Message is too long (max {maxLength} characters)." });

        ChatSession? session = null;
        if (sessionId.HasValue)
        {
            session = await _db.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == UserId);
            if (session is null) return NotFound(new { error = "Chat not found." });
        }

        bool isNewSession = session is null;
        if (isNewSession)
        {
            session = new ChatSession { UserId = UserId };
            _db.ChatSessions.Add(session);
        }

        session!.Messages.Add(new ChatMessage { Role = "user", Content = message });
        session.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Send recent history so the model has conversation context, within token limits.
        var history = await _db.ChatMessages
            .Where(m => m.ChatSessionId == session.Id)
            .OrderByDescending(m => m.Id)
            .Take(20)
            .OrderBy(m => m.Id)
            .ToListAsync();

        string reply;
        try
        {
            reply = await _gemini.GetChatResponseAsync(history);
        }
        catch
        {
            return StatusCode(502, new { error = "The assistant is unavailable right now. Please try again in a moment." });
        }

        session.Messages.Add(new ChatMessage { Role = "model", Content = reply });
        session.UpdatedAt = DateTime.UtcNow;

        if (isNewSession)
            session.Title = await _gemini.GenerateTitleAsync(message);

        await _db.SaveChangesAsync();

        return Json(new { sessionId = session.Id, title = session.Title, reply });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(int sessionId, string title)
    {
        title = (title ?? string.Empty).Trim();
        if (title.Length is 0 or > 120)
            return BadRequest(new { error = "Title must be between 1 and 120 characters." });

        var session = await _db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == UserId);
        if (session is null) return NotFound();

        session.Title = title;
        await _db.SaveChangesAsync();

        return Json(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int sessionId)
    {
        var session = await _db.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == UserId);
        if (session is null) return NotFound();

        _db.ChatSessions.Remove(session);
        await _db.SaveChangesAsync();

        return Json(new { ok = true });
    }
}
