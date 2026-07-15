using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using SemiColon.ai_MVC.Controllers;
using SemiColon.ai_MVC.Data;
using SemiColon.ai_MVC.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication: main cookie + temporary external cookie for Google
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
    })
    .AddCookie(AccountController.ExternalScheme)
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = builder.Configuration["GoogleAuth:ClientId"] ?? string.Empty;
        options.ClientSecret = builder.Configuration["GoogleAuth:ClientSecret"] ?? string.Empty;
        options.SignInScheme = AccountController.ExternalScheme;
        options.ClaimActions.MapJsonKey("picture", "picture");
    });

// Rate limiting: per-user fixed window on chat messages
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("chat", context =>
    {
        string key = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue<int>("RateLimit:MessagesPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\":\"You are sending messages too quickly. Please wait a moment.\"}", cancellationToken);
    };
});

// App services
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddHttpClient<GeminiService>(client => client.Timeout = TimeSpan.FromSeconds(60));

var app = builder.Build();

// Create the database and tables on first run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
