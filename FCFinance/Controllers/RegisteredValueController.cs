
using FCFinance.Auth;
using FCFinance.Models;
using FCFinance.DTO;
using FCFinance.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    // Lançamentos (receitas/despesas). Cada usuário só vê e altera os próprios.
    [ApiController]
    [Route("api/[controller]")]
    public class RegisteredValueController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RegisteredValueService _values;

        public RegisteredValueController(AppDbContext appContext, RegisteredValueService values)
        {
            _context = appContext;
            _values = values;
        }

        private IQueryable<RegisteredValue> Mine()
        {
            var userId = User.GetUserId();
            return _context.RegisteredValues.Where(x => x.UserId == userId);
        }

        // Converte para o formato de saída direto na consulta (traz só as colunas necessárias).
        private static IQueryable<RegisteredValueResponseDTO> ToResponse(IQueryable<RegisteredValue> query) =>
            query.Select(x => new RegisteredValueResponseDTO
            {
                Id = x.Id,
                Name = x.Name,
                Value = x.Value,
                CreateWhen = x.CreateWhen,
                CategoryId = x.CategoryId,
                ValueTypeId = x.ValueTypeId,
                Category = new CategorySummaryDTO { Id = x.Category.Id, Name = x.Category.Name },
                ValueType = new ValueTypeDTO { Id = x.ValueType.Id, Name = x.ValueType.Name }
            });

        private Task<RegisteredValueResponseDTO> LoadResponseAsync(int id) =>
            ToResponse(_context.RegisteredValues.Where(x => x.Id == id)).SingleAsync();

        // LISTAR LANÇAMENTOS
        // Todos os filtros são opcionais. Sem nenhum, devolve tudo (é o que o site usa hoje).
        //   from / to      -> período (datas inclusivas, ex.: ?from=2026-01-01&to=2026-01-31)
        //   categoryId     -> só de uma categoria
        //   valueTypeId    -> 1 = receitas, 2 = despesas
        //   page/pageSize  -> paginação (o total vem no cabeçalho X-Total-Count)
        [HttpGet]
        public async Task<IActionResult> ListRegisteredValue(
            [FromQuery] DateTime? from, [FromQuery] DateTime? to,
            [FromQuery] int? categoryId, [FromQuery] int? valueTypeId,
            [FromQuery] int? page, [FromQuery] int? pageSize)
        {
            var query = Mine();

            if (from.HasValue) query = query.Where(x => x.CreateWhen >= from.Value.Date);
            if (to.HasValue) query = query.Where(x => x.CreateWhen < to.Value.Date.AddDays(1));
            if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId.Value);
            if (valueTypeId.HasValue) query = query.Where(x => x.ValueTypeId == valueTypeId.Value);

            query = query.OrderByDescending(x => x.CreateWhen).ThenByDescending(x => x.Id);

            if (page.HasValue || pageSize.HasValue)
            {
                var size = Math.Clamp(pageSize ?? 20, 1, 200);
                var number = Math.Max(page ?? 1, 1);
                Response.Headers["X-Total-Count"] = (await query.CountAsync()).ToString();
                query = query.Skip((number - 1) * size).Take(size);
            }

            var registeredValues = await ToResponse(query).ToListAsync();

            return Ok(registeredValues);
        }

        // BUSCAR LANÇAMENTO POR ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRegisteredValueById(int id)
        {
            var registeredValue = await ToResponse(Mine().Where(x => x.Id == id))
                .SingleOrDefaultAsync();

            if (registeredValue == null)
                return NotFound("Registro não encontrado.");

            return Ok(registeredValue);
        }

        // BUSCAR LANÇAMENTO POR ID E TIPO (mantido por compatibilidade; prefira GET {id})
        [HttpGet("{id}/{type}")]
        public async Task<IActionResult> GetRegisteredValue(int id, int type)
        {
            var registeredValue = await ToResponse(Mine().Where(x => x.Id == id && x.ValueTypeId == type))
                .SingleOrDefaultAsync();

            if (registeredValue == null)
                return NotFound("Registro não encontrado.");

            return Ok(registeredValue);
        }

        // CADASTRAR LANÇAMENTO
        [HttpPost]
        public async Task<IActionResult> SetRegisteredValue(RegisteredValueDTO registeredValue)
        {
            var (newValue, error) = await _values.CreateAsync(User.GetUserId(), registeredValue);
            if (newValue == null) return BadRequest(error);

            return CreatedAtAction(nameof(GetRegisteredValueById), new { id = newValue.Id }, await LoadResponseAsync(newValue.Id));
        }

        // ATUALIZAR LANÇAMENTO
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRegisteredValue(
            int id, RegisteredValueDTO registeredValue)
        {
            var existingValue = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (existingValue == null)
                return NotFound("Registro não encontrado.");

            var error = await _values.ValidateAsync(User.GetUserId(), registeredValue);
            if (error != null) return BadRequest(error);

            existingValue.Name = registeredValue.Name.Trim();
            existingValue.Value = registeredValue.Value;
            existingValue.CategoryId = registeredValue.CategoryId;
            existingValue.ValueTypeId = registeredValue.ValueTypeId;
            existingValue.CreateWhen = registeredValue.CreateWhen;

            await _context.SaveChangesAsync();

            return Ok(await LoadResponseAsync(id));
        }

        // EXCLUIR LANÇAMENTO
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegisteredValue(int id)
        {
            var registeredValue = await Mine().SingleOrDefaultAsync(x => x.Id == id);

            if (registeredValue == null)
                return NotFound("Registro não encontrado.");

            _context.RegisteredValues.Remove(registeredValue);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
