using System.ComponentModel.DataAnnotations;

namespace SemiColon.ai_MVC.Models.ViewModels;

public class RegisterViewModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must be 8+ characters with a capital letter, a number and a special character.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class VerifyOtpViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Enter the 6-digit code.")]
    [Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
        ErrorMessage = "Password must be 8+ characters with a capital letter, a number and a special character.")]
    [Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
