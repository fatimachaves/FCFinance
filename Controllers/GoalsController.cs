using FCFinance.Auth;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    // Metas. Cada usuário só vê e altera as próprias, e tem no máximo UMA meta prioritária.
    [ApiController]
    [Route("api/[controller]")]
    public class GoalsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GoalsController(AppDbContext appContext)
        {
            _context = appContext;
        }

        private IQueryable<Goal> Mine()
        {
            var userId = User.GetUserId();
            return _context.Goals.Where(g => g.UserId == userId);
        }

        // LISTAR METAS
        [HttpGet]
        public async Task<IActionResult> ListGoals()
        {
            var goals = await Mine().OrderByDescending(g => g.IsPriority).ThenBy(g => g.Name).ToListAsync();

            return Ok(goals.Select(GoalResponseDTO.From));
        }

        // BUSCAR A META PRIORITÁRIA (204 se nenhuma foi definida)
        [HttpGet("priority")]
        public async Task<IActionResult> GetPriorityGoal()
        {
            var goal = await Mine().SingleOrDefaultAsync(x => x.IsPriority);

            if (goal == null)
                return NoContent();

            return Ok(GoalResponseDTO.From(goal));
        }

        // BUSCAR META POR ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGoal(int id)
        {
            var goal = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (goal == null)
                return NotFound("Registro não encontrado.");

            return Ok(GoalResponseDTO.From(goal));
        }

        // CADASTRAR META
        [HttpPost]
        public async Task<IActionResult> SetGoal(GoalDTO goal)
        {
            var newValue = new Goal()
            {
                Name = goal.Name.Trim(),
                TargetValue = goal.TargetValue,
                CurrentValue = goal.CurrentValue,
                CreatedWhen = DateTime.UtcNow, // definido pela API, não pelo cliente
                IsMonthly = goal.IsMonthly,
                ReferenceDate = goal.ReferenceDate,
                EndDate = goal.EndDate,
                IsPriority = goal.IsPriority,
                UserId = User.GetUserId()
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (newValue.IsPriority)
                await ClearPriorityAsync();

            _context.Goals.Add(newValue);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetGoal), new { id = newValue.Id }, GoalResponseDTO.From(newValue));
        }

        // ATUALIZAR META
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGoal(int id, GoalDTO goal)
        {
            var existingValue = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (existingValue == null)
                return NotFound("Registro não encontrado.");

            existingValue.Name = goal.Name.Trim();
            existingValue.TargetValue = goal.TargetValue;
            existingValue.CurrentValue = goal.CurrentValue;
            // CreatedWhen não muda na edição.
            existingValue.IsMonthly = goal.IsMonthly;
            existingValue.ReferenceDate = goal.ReferenceDate;
            existingValue.EndDate = goal.EndDate;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            if (goal.IsPriority && !existingValue.IsPriority)
                await ClearPriorityAsync(exceptId: id);
            existingValue.IsPriority = goal.IsPriority;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(GoalResponseDTO.From(existingValue));
        }

        // DEFINIR COMO META PRIORITÁRIA (a anterior deixa de ser)
        [HttpPut("{id}/priority")]
        public async Task<IActionResult> SetPriority(int id)
        {
            var goal = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (goal == null)
                return NotFound("Registro não encontrado.");

            if (!goal.IsPriority)
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                await ClearPriorityAsync(exceptId: id);
                goal.IsPriority = true;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }

            return Ok(GoalResponseDTO.From(goal));
        }

        // REMOVER A PRIORIDADE DA META
        [HttpDelete("{id}/priority")]
        public async Task<IActionResult> RemovePriority(int id)
        {
            var goal = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (goal == null)
                return NotFound("Registro não encontrado.");

            goal.IsPriority = false;
            await _context.SaveChangesAsync();

            return Ok(GoalResponseDTO.From(goal));
        }

        // EXCLUIR META
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGoal(int id)
        {
            var goal = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (goal == null)
                return NotFound("Registro não encontrado.");

            _context.Goals.Remove(goal);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Tira a prioridade das metas do usuário logado (menos a informada). Roda direto no banco,
        // antes de gravar a nova prioritária, para não violar o índice único.
        private Task<int> ClearPriorityAsync(int? exceptId = null) =>
            Mine()
                .Where(g => g.IsPriority && g.Id != exceptId)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsPriority, false));
    }
}
