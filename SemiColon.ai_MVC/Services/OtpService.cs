using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SemiColon.ai_MVC.Data;
using SemiColon.ai_MVC.Models;

namespace SemiColon.ai_MVC.Services;

public class OtpService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public OtpService(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task GenerateAndSendAsync(AppUser user, OtpPurpose purpose)
    {
        // Invalidate any previous unused codes for the same purpose.
        var oldCodes = await _db.OtpCodes
            .Where(o => o.UserId == user.Id && o.Purpose == purpose && !o.IsUsed)
            .ToListAsync();
        _db.OtpCodes.RemoveRange(oldCodes);

        string code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        _db.OtpCodes.Add(new OtpCode
        {
            UserId = user.Id,
            Code = code,
            Purpose = purpose,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        });
        await _db.SaveChangesAsync();

        string purposeText = purpose == OtpPurpose.Signup ? "sign up" : "sign in";
        await _email.SendOtpAsync(user.Email, code, purposeText);
    }

    public async Task<bool> VerifyAsync(int userId, string code, OtpPurpose purpose)
    {
        var otp = await _db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.IsUsed)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (otp is null || otp.ExpiresAt < DateTime.UtcNow || otp.FailedAttempts >= 5)
            return false;

        if (otp.Code != code)
        {
            otp.FailedAttempts++;
            await _db.SaveChangesAsync();
            return false;
        }

        otp.IsUsed = true;
        await _db.SaveChangesAsync();
        return true;
    }
}
