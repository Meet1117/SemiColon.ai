using System.Net;
using System.Net.Mail;

namespace SemiColon.ai_MVC.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var smtp = _config.GetSection("Smtp");

        using var client = new SmtpClient(smtp["Host"], smtp.GetValue<int>("Port"))
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtp["Username"], smtp["Password"])
        };

        using var message = new MailMessage
        {
            From = new MailAddress(smtp["FromEmail"]!, smtp["FromName"]),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message);
    }

    public Task SendOtpAsync(string toEmail, string code, string purpose)
    {
        string body = $@"
            <div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;padding:32px;border:1px solid #e5e5e5;border-radius:12px;'>
                <h2 style='margin:0 0 8px;color:#18181b;'>SemiColon.ai</h2>
                <p style='color:#52525b;'>Use the code below to complete your {purpose}. It expires in <b>5 minutes</b>.</p>
                <div style='font-size:32px;font-weight:700;letter-spacing:8px;text-align:center;padding:16px;background:#f4f4f5;border-radius:8px;color:#18181b;'>{code}</div>
                <p style='color:#a1a1aa;font-size:12px;margin-top:16px;'>If you didn't request this, you can safely ignore this email.</p>
            </div>";

        return SendAsync(toEmail, $"{code} is your SemiColon.ai verification code", body);
    }

    public Task SendPasswordResetAsync(string toEmail, string resetLink)
    {
        string body = $@"
            <div style='font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:0 auto;padding:32px;border:1px solid #e5e5e5;border-radius:12px;'>
                <h2 style='margin:0 0 8px;color:#18181b;'>SemiColon.ai</h2>
                <p style='color:#52525b;'>We received a request to reset your password. Click the button below. The link expires in <b>1 hour</b>.</p>
                <p style='text-align:center;margin:24px 0;'>
                    <a href='{resetLink}' style='background:#18181b;color:#fff;text-decoration:none;padding:12px 28px;border-radius:8px;display:inline-block;'>Reset password</a>
                </p>
                <p style='color:#a1a1aa;font-size:12px;'>If you didn't request this, you can safely ignore this email.</p>
            </div>";

        return SendAsync(toEmail, "Reset your SemiColon.ai password", body);
    }
}
