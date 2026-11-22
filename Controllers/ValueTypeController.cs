using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    // Tipos de lançamento (Receita / Despesa). Somente leitura: são fixos do sistema.
    [ApiController]
    [Route("api/[controller]")]
    public class ValueTypeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ValueTypeController(AppDbContext context) => _context = context;

        // LISTAR TIPOS
        [HttpGet]
        public async Task<IActionResult> ListValueTypes()
        {
            var types = await _context.ValueTypes
                .OrderBy(t => t.Id)
                .Select(t => new ValueTypeDTO { Id = t.Id, Name = t.Name })
                .ToListAsync();

            return Ok(types);
        }
    }
}
