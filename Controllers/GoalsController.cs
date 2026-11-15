using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : Controller
    {
        private readonly AppDbContext _context;

        public GoalsController(AppDbContext appContext)
        {
            _context = appContext;
        }

        // LISTAR LANÇAMENTOS
        [HttpGet]
        public async Task<IActionResult> ListGoals()
        {
            var goals = await _context.Goals.ToListAsync();

            return Ok(goals);
        }

        // BUSCAR LANÇAMENTO POR ID E TIPO
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGoal(int id)
        {
            var goals = await _context.Goals.SingleOrDefaultAsync(x =>x.Id == id);

            if (goals == null)
                return NotFound("Registro não encontrado.");

            return Ok(goals);
        }

        // CADASTRAR LANÇAMENTO
        [HttpPost]
        public async Task<IActionResult> SetGoal(Goal goal)
        {
            var newValue = new Goal()
            {
                Name = goal.Name,
                TargetValue = goal.TargetValue,
                CurrentValue = goal.CurrentValue,
                CreatedWhen = goal.CreatedWhen,
                IsMonthly = goal.IsMonthly,
                ReferenceDate = goal.ReferenceDate,
                EndDate = goal.EndDate
            };

            _context.Goals.Add(newValue);

            await _context.SaveChangesAsync();

            return Ok(newValue);
        }

        // ATUALIZAR LANÇAMENTO
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGoal(int id, Goal goal)
        {
            var existingValue = await _context.Goals.FindAsync(id);

            if (existingValue == null)
                return NotFound("Registro não encontrado.");

            existingValue.Name = goal.Name;
            existingValue.TargetValue = goal.TargetValue;
            existingValue.CurrentValue = goal.CurrentValue;
            existingValue.CreatedWhen = goal.CreatedWhen;
            existingValue.IsMonthly = goal.IsMonthly;
            existingValue.ReferenceDate = goal.ReferenceDate;
            existingValue.EndDate = goal.EndDate;

            await _context.SaveChangesAsync();

            return Ok(existingValue);
        }

        // EXCLUIR LANÇAMENTO
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            var goals = await _context.Goals.FindAsync(id);

            if (goals == null)
                return NotFound("Registro não encontrado.");

            _context.Goals.Remove(goals);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
