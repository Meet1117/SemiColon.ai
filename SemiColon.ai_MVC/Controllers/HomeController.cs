using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SemiColon.ai_MVC.Models;

namespace SemiColon.ai_MVC.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Chat")
            : RedirectToAction("Login", "Account");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
