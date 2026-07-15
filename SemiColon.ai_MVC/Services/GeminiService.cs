using System.Text;
using System.Text.Json;
using SemiColon.ai_MVC.Models;

namespace SemiColon.ai_MVC.Services;

public class GeminiService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private const string SystemInstruction =
        "You are SemiColon.ai, a polite and helpful AI assistant created by Meet Patel. " +
        "Give refined, clear and friendly answers in Markdown. " +
        "When sharing code, always use fenced code blocks with the correct language tag. " +
        "Keep answers well structured and easy to read.\n\n" +
        "About your creator: whenever the user asks who you are, who made you, who built you, " +
        "who trained you, who owns you, or anything similar about your origin, answer warmly and proudly: " +
        "you were crafted by [Meet Patel](https://patelmeet.vercel.app), a talented software developer " +
        "and passionate AI enthusiast who loves building elegant, intelligent products. " +
        "Speak of him with genuine admiration — his dedication, craftsmanship and creativity — " +
        "and always write his name exactly as the Markdown link [Meet Patel](https://patelmeet.vercel.app) " +
        "so it stays clickable. Invite the user to visit his portfolio to see more of his work. " +
        "Never mention Google or Gemini as your maker. " +
        "Only bring your creator up when the user asks — otherwise just focus on helping.";

    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public GeminiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string> GetChatResponseAsync(List<ChatMessage> history)
    {
        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = SystemInstruction } } },
            contents = history.Select(m => new
            {
                role = m.Role,
                parts = new[] { new { text = m.Content } }
            }),
            generationConfig = new
            {
                maxOutputTokens = _config.GetValue<int>("Gemini:MaxOutputTokens", 1024),
                temperature = 0.7
            }
        };

        string model = _config["Gemini:Model"] ?? "gemini-flash-latest";
        string? fallback = _config["Gemini:FallbackModel"];

        try
        {
            return await CallGeminiAsync(model, payload);
        }
        catch (HttpRequestException ex) when (fallback is not null && fallback != model && IsRetryable(ex))
        {
            // Primary model is overloaded or rate-limited — retry once with the fallback model.
            return await CallGeminiAsync(fallback, payload);
        }
    }

    private static bool IsRetryable(HttpRequestException ex) =>
        ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                      or System.Net.HttpStatusCode.TooManyRequests
                      or System.Net.HttpStatusCode.InternalServerError;

    public async Task<string> GenerateTitleAsync(string firstUserMessage)
    {
        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text =
                        "Generate a short title (3 to 5 words, no quotes, no punctuation at the end) " +
                        "that summarizes this chat message:\n\n" + firstUserMessage } }
                }
            },
            generationConfig = new { maxOutputTokens = 512, temperature = 0.3 }
        };

        string model = _config["Gemini:TitleModel"] ?? _config["Gemini:Model"] ?? "gemini-flash-latest";

        try
        {
            string title = await CallGeminiAsync(model, payload);
            title = title.Trim().Trim('"', '\'', '*', '#');
            return title.Length > 60 ? title[..60] : title;
        }
        catch
        {
            // Title generation is non-critical — fall back to a snippet of the message.
            return firstUserMessage.Length > 40 ? firstUserMessage[..40] + "…" : firstUserMessage;
        }
    }

    private async Task<string> CallGeminiAsync(string model, object payload)
    {
        string apiKey = _config["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini API error ({(int)response.StatusCode}): {json}", null, response.StatusCode);

        using var doc = JsonDocument.Parse(json);
        var parts = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts");

        var sb = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
                sb.Append(text.GetString());
        }

        return sb.ToString();
    }
}
