using FCFinance.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FCFinance.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // Páginas legais: abertas para todos (o cadastro pede o aceite delas).
        [AllowAnonymous]
        public IActionResult Termos() => View();

        [AllowAnonymous]
        public IActionResult Privacidade() => View();

        [AllowAnonymous] // a página de erro precisa abrir mesmo sem login
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
