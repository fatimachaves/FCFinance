using Microsoft.AspNetCore.Mvc;

namespace FCFinance.Web.Controllers
{
    // Tela "WhatsApp": conectar o telefone ao bot e simulador de conversa.
    // Os dados vêm da API (api/WhatsApp), chamada pelo JavaScript da página (wwwroot/js/whatsapp.js).
    public class WhatsAppController : Controller
    {
        public IActionResult Index() => View();
    }
}
