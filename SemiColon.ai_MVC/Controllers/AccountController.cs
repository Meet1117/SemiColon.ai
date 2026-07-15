using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SemiColon.ai_MVC.Data;
using SemiColon.ai_MVC.Models;
using SemiColon.ai_MVC.Models.ViewModels;
using SemiColon.ai_MVC.Services;

namespace SemiColon.ai_MVC.Controllers;

public class AccountController : Controller
{
    public const string ExternalScheme = "External";

    // After a successful OTP, password logins skip the email code for this long.
    private static readonly TimeSpan OtpTrustWindow = TimeSpan.FromHours(2);

    private readonly AppDbContext _db;
    private readonly OtpService _otp;
    private readonly EmailService _email;
    private readonly IWebHostEnvironment _env;

    public AccountController(AppDbContext db, OtpService otp, EmailService email, IWebHostEnvironment env)
    {
        _db = db;
        _otp = otp;
        _email = email;
        _env = env;
    }

    // ---------- Register ----------

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string email = model.Email.Trim().ToLowerInvariant();
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (existing is not null && existing.IsEmailVerified)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        // Re-registering an unverified account just refreshes it.
        var user = existing ?? new AppUser { Email = email };
        user.FullName = model.FullName.Trim();
        user.PasswordHash = PasswordHasher.Hash(model.Password);

        if (existing is null) _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _otp.GenerateAndSendAsync(user, OtpPurpose.Signup);

        return RedirectToAction(nameof(VerifyOtp), new { email = user.Email, purpose = OtpPurpose.Signup });
    }

    // ---------- Login ----------

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string email = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user?.PasswordHash is null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password. Please check your details and try again.");
            return View(model);
        }

        // Recently verified via OTP? Trust the device and sign straight in.
        if (user.IsEmailVerified && user.LastOtpVerifiedAt > DateTime.UtcNow - OtpTrustWindow)
        {
            await SignInUserAsync(user);
            SetToast($"Welcome back, {FirstName(user)}!");
            return RedirectToAction("Index", "Chat");
        }

        var purpose = user.IsEmailVerified ? OtpPurpose.Login : OtpPurpose.Signup;
        await _otp.GenerateAndSendAsync(user, purpose);

        return RedirectToAction(nameof(VerifyOtp), new { email = user.Email, purpose });
    }

    // Live email-availability check used by the registration form.
    [HttpGet]
    public async Task<IActionResult> CheckEmail(string email)
    {
        string normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        bool taken = await _db.Users.AnyAsync(u => u.Email == normalized && u.IsEmailVerified);
        return Json(new { taken });
    }

    // ---------- OTP verification ----------

    [HttpGet]
    public IActionResult VerifyOtp(string email, OtpPurpose purpose)
    {
        if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(Login));
        return View(new VerifyOtpViewModel { Email = email, Purpose = purpose });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string email = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !await _otp.VerifyAsync(user.Id, model.Code.Trim(), model.Purpose))
        {
            ModelState.AddModelError(nameof(model.Code), "Invalid or expired code. Please try again.");
            return View(model);
        }

        bool isNewAccount = !user.IsEmailVerified;
        user.IsEmailVerified = true;
        user.LastOtpVerifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await SignInUserAsync(user);
        SetToast(isNewAccount
            ? $"Welcome to SemiColon.ai, {FirstName(user)}! Your account is ready."
            : $"Welcome back, {FirstName(user)}!");

        return RedirectToAction("Index", "Chat");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string email, OtpPurpose purpose)
    {
        string normalized = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized);

        if (user is not null)
            await _otp.GenerateAndSendAsync(user, purpose);

        TempData["Info"] = "A new code has been sent to your email.";
        return RedirectToAction(nameof(VerifyOtp), new { email, purpose });
    }

    // ---------- Google login ----------

    [HttpGet]
    public IActionResult GoogleLogin()
    {
        var props = new AuthenticationProperties { RedirectUri = Url.Action(nameof(GoogleCallback)) };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(ExternalScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            TempData["Error"] = "Google sign-in failed. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        string? googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? email = result.Principal.FindFirstValue(ClaimTypes.Email)?.ToLowerInvariant();
        string name = result.Principal.FindFirstValue(ClaimTypes.Name) ?? "User";
        string? picture = result.Principal.FindFirstValue("picture");

        if (googleId is null || email is null)
        {
            TempData["Error"] = "Google did not provide the required information.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId || u.Email == email);
        bool isNewAccount = user is null;

        if (user is null)
        {
            user = new AppUser { FullName = name, Email = email, GoogleId = googleId, IsEmailVerified = true };
            _db.Users.Add(user);
        }
        else
        {
            // Link Google to an existing custom account.
            user.GoogleId ??= googleId;
            user.IsEmailVerified = true;
        }

        // Use the Google profile photo unless the user set their own.
        if (picture is not null && (user.AvatarUrl is null || user.AvatarUrl.StartsWith("http")))
            user.AvatarUrl = picture;

        await _db.SaveChangesAsync();

        await HttpContext.SignOutAsync(ExternalScheme);
        await SignInUserAsync(user);
        SetToast(isNewAccount
            ? $"Welcome to SemiColon.ai, {FirstName(user)}! Your account is ready."
            : $"Welcome back, {FirstName(user)}!");

        return RedirectToAction("Index", "Chat");
    }

    // ---------- Forgot / reset password ----------

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string email = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Always show the same message so emails can't be enumerated.
        if (user is not null)
        {
            string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await _db.SaveChangesAsync();

            string link = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme)!;
            await _email.SendPasswordResetAsync(user.Email, link);
        }

        TempData["Info"] = "If an account exists for that email, a reset link has been sent.";
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (!await IsResetTokenValidAsync(token))
        {
            TempData["Error"] = "This reset link is invalid or has expired.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var reset = await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == model.Token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

        if (reset is null)
        {
            TempData["Error"] = "This reset link is invalid or has expired.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        reset.User.PasswordHash = PasswordHasher.Hash(model.Password);
        reset.IsUsed = true;
        await _db.SaveChangesAsync();

        TempData["Info"] = "Your password has been reset. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // ---------- Logout ----------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        SetToast("You have been signed out. See you soon!");
        return RedirectToAction(nameof(Login));
    }

    // ---------- Profile (edited from the modal on the chat page) ----------

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(string fullName)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        fullName = (fullName ?? string.Empty).Trim();
        if (fullName.Length is 0 or > 100)
        {
            SetToast("Please enter a valid name (up to 100 characters).", "error");
            return RedirectToAction("Index", "Chat");
        }

        user.FullName = fullName;
        await _db.SaveChangesAsync();

        // Refresh the cookie so the new name shows everywhere.
        await SignInUserAsync(user);

        SetToast("Your profile has been updated.");
        return RedirectToAction("Index", "Chat");
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile? photo)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        string[] allowed = [".jpg", ".jpeg", ".png", ".webp"];
        string ext = Path.GetExtension(photo?.FileName ?? string.Empty).ToLowerInvariant();

        if (photo is null || photo.Length == 0 || !allowed.Contains(ext))
        {
            SetToast("Please choose a JPG, PNG or WEBP image.", "error");
            return RedirectToAction("Index", "Chat");
        }
        if (photo.Length > 2 * 1024 * 1024)
        {
            SetToast("The image must be smaller than 2 MB.", "error");
            return RedirectToAction("Index", "Chat");
        }

        string folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(folder);

        DeleteUploadedAvatar(user);

        string fileName = $"u{user.Id}_{DateTime.UtcNow.Ticks}{ext}";
        await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
        {
            await photo.CopyToAsync(stream);
        }

        user.AvatarUrl = $"/uploads/avatars/{fileName}";
        await _db.SaveChangesAsync();

        SetToast("Profile photo updated.");
        return RedirectToAction("Index", "Chat");
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));

        DeleteUploadedAvatar(user);
        user.AvatarUrl = null;
        await _db.SaveChangesAsync();

        SetToast("Profile photo removed.");
        return RedirectToAction("Index", "Chat");
    }

    // ---------- Helpers ----------

    private Task<AppUser?> GetCurrentUserAsync()
    {
        int id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return _db.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    private void DeleteUploadedAvatar(AppUser user)
    {
        // Only local uploads live on disk — Google picture URLs have nothing to delete.
        if (user.AvatarUrl is null || !user.AvatarUrl.StartsWith("/uploads/")) return;

        string path = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private void SetToast(string message, string type = "success")
    {
        TempData["Toast"] = message;
        TempData["ToastType"] = type;
    }

    private static string FirstName(AppUser user) =>
        user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? user.FullName;

    private Task<bool> IsResetTokenValidAsync(string token) =>
        _db.PasswordResetTokens.AnyAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow);

    private Task SignInUserAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) };

        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), props);
    }
}
