using Microsoft.AspNetCore.Mvc;

namespace FCFinance.Web.Controllers;

// Qualquer usuário logado (Admin ou Usuario) acessa. O nome no "Olá, ..." vem do login (@User.Nome()).
public class TransacoesController : Controller
{
    // GET /Transacoes            -> abre em "Receita"
    // GET /Transacoes?tipo=despesa -> abre em "Despesa"
    public IActionResult Index() => View();
}
