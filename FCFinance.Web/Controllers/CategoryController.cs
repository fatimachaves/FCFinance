using FCFinance.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FCFinance.Web.Controllers
{
    [Authorize(Roles = Perfis.Admin)] // página antiga: só administrador
    public class CategoryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
