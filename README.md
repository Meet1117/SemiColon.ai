# SemiColon.ai `;`

A clean, minimal AI chatbot built with **ASP.NET Core MVC** — think of it as your own little ChatGPT, powered by **Google Gemini**, with a proper login system, email OTP verification, chat history, and a UI that stays out of your way.

Built with ❤️ by [Meet Patel](https://patelmeet.vercel.app) — Software Developer & AI Enthusiast.

---

## ✨ What can it do?

**Chat**
- Ask anything and get polite, well-formatted answers from Gemini.
- Replies stream in with a smooth typing animation — just like the big AI apps.
- Ask for code and you get a **VS Code-style dark code block** with syntax colors and a one-click **Copy code** button.
- Every new chat gets an **automatic title** based on what you asked.
- Rename or delete any chat right from the sidebar (with a custom confirm dialog — no ugly browser popups).
- Scroll up to read something? A little arrow appears to take you back down.
- Time-based greeting when you start — "Good morning", "Hello, night owl 🌙" and friends.

**Accounts & security**
- Sign up with email + password, verified with a **6-digit OTP sent to your email** (expires in 5 minutes).
- Login also asks for an OTP — but once verified, you're **trusted for 2 hours** and can log in with just your password.
- Or skip all that and **sign in with Google** in one click (it even grabs your Google profile picture).
- Forgot your password? A **reset link** lands in your inbox, valid for 1 hour.
- Live form validation: password strength checklist, "email already registered" warnings, and matching-password checks as you type.
- A fun `:` / `;` toggle on password fields to show/hide what you typed.

**Extras**
- Profile modal — change your name, upload a profile photo (or remove it), all without leaving the chat.
- Toast notifications in the top-right for logins, logouts, saves, and errors.
- **Rate limiting** — 10 messages per minute per user, so nobody can spam the API (and your Gemini bill stays tiny).
- Automatic **fallback model** — if the main Gemini model is overloaded, the app quietly retries with a backup one.

---

## 🧰 Tech stack

| Layer | What's used |
|---|---|
| Backend | ASP.NET Core 8 MVC (C#) |
| Database | SQL Server + Entity Framework Core |
| AI | Google Gemini API (configurable models) |
| Auth | Cookie auth, email OTP, Google OAuth |
| Email | Plain SMTP (works great with Gmail) |
| Frontend | Razor views, vanilla JS, custom CSS — no heavy frameworks |
| Markdown & code | marked.js + highlight.js + DOMPurify |

---

## 🚀 How to run it (step by step)

### 1. What you need first
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **SQL Server** (Express or LocalDB is fine)
- A **Gmail account** (for sending OTP emails)
- A **Google Cloud** account (for Google login)
- A **Gemini API key** (free at [Google AI Studio](https://aistudio.google.com))

### 2. Get the code and restore packages
```bash
git clone <your-repo-url>
cd SemiColon.ai_MVC
dotnet restore
```

### 3. Set up your secrets in `appsettings.json`
Open `appsettings.json` and fill in each section:

**Database** — point it at your SQL Server:
```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=SemiColonAi;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True"
```
> `.\SQLEXPRESS` means "the SQL Express instance on my machine". Change it to match yours. The database and tables are **created automatically** the first time you run the app — no migrations to run. 🎉

**Email (SMTP)** — used to send OTP codes and reset links:
1. Go to your Google Account → **Security** → turn on **2-Step Verification**.
2. Then search for **App passwords** and create one.
3. Paste it in:
```json
"Smtp": {
  "Host": "smtp.gmail.com",
  "Port": 587,
  "Username": "you@gmail.com",
  "Password": "your-16-char-app-password",
  "FromEmail": "you@gmail.com",
  "FromName": "SemiColon.ai"
}
```
> ⚠️ Use the **App Password**, not your real Gmail password.

**Google login**:
1. Go to [Google Cloud Console](https://console.cloud.google.com) → APIs & Services → **Credentials**.
2. Create an **OAuth 2.0 Client ID** (type: Web application).
3. Add these:
   - Authorised JavaScript origins: `https://localhost:7092` and `http://localhost:5071`
   - Authorised redirect URIs: `https://localhost:7092/signin-google` and `http://localhost:5071/signin-google`
4. Copy the Client ID & Secret into the `GoogleAuth` section.

**Gemini**:
1. Grab a free API key from [Google AI Studio](https://aistudio.google.com).
2. Paste it into the `Gemini` section. The models are already configured — change them anytime without touching code.

### 4. Run it!
```bash
dotnet run --launch-profile https
```
Then open **https://localhost:7092** in your browser.

(Using Visual Studio instead? Just press **F5** with the `https` profile.)

### 5. Try it out
1. Create an account → check your inbox for the OTP → verify.
2. Say hi to the chatbot. Ask it for some code. Ask it *who made it* 😉
3. Rename a chat, upload a profile photo, log out and back in — everything just works.

---

## 🗂️ Project structure (quick tour)

```
Controllers/     → AccountController (auth, OTP, profile), ChatController (chat + history)
Services/        → GeminiService (AI calls), EmailService (SMTP), OtpService, PasswordHasher
Models/          → Entities (AppUser, ChatSession, ChatMessage, OtpCode…) + ViewModels
Data/            → AppDbContext (EF Core)
Views/           → Razor pages for auth, chat, and shared partials
wwwroot/         → css/ and js/ — all the custom styling and chat logic
appsettings.json → all your keys and settings live here
```

---

## 🔒 A quick note on secrets

`appsettings.json` holds real credentials. If you ever push this project to a public repo, **don't commit your keys** — use [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or add the file to `.gitignore`.

---

Made with plenty of semicolons `;` by [Meet Patel](https://patelmeet.vercel.app)
