
using FCFinance.Models;
using FCFinance.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegisteredValueController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RegisteredValueController(AppDbContext appContext)
        {
            _context = appContext;
        }

        // LISTAR LANÇAMENTOS
        [HttpGet]
        public async Task<IActionResult> ListRegisteredValue()
        {
            var registeredValues = await _context.RegisteredValues
                .Include(x => x.Category)
                .ToListAsync();

            return Ok(registeredValues);
        }

        // BUSCAR LANÇAMENTO POR ID E TIPO
        [HttpGet("{id}/{type}")]
        public async Task<IActionResult> GetRegisteredValue(int id, int type)
        {
            var registeredValue = await _context.RegisteredValues
                .SingleOrDefaultAsync(x =>
                    x.Id == id && x.ValueTypeId == type);

            if (registeredValue == null)
                return NotFound("Registro não encontrado.");

            return Ok(registeredValue);
        }

        // CADASTRAR LANÇAMENTO
        [HttpPost]
        public async Task<IActionResult> SetRegisteredValue(RegisteredValueDTO registeredValue)
        {
            var newValue = new RegisteredValue()
            {
                Name = registeredValue.Name,
                Value = registeredValue.Value,
                CategoryId = registeredValue.CategoryId,
                ValueTypeId = registeredValue.ValueTypeId,
                CreateWhen = registeredValue.CreateWhen
            };

            _context.RegisteredValues.Add(newValue);

            await _context.SaveChangesAsync();

            return Ok(newValue);
        }

        // ATUALIZAR LANÇAMENTO
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRegisteredValue(
            int id, RegisteredValueDTO registeredValue)
        {
            var existingValue = await _context.RegisteredValues
                .FindAsync(id);

            if (existingValue == null)
                return NotFound("Registro não encontrado.");

            existingValue.Name = registeredValue.Name;
            existingValue.Value = registeredValue.Value;
            existingValue.CategoryId = registeredValue.CategoryId;
            existingValue.ValueTypeId = registeredValue.ValueTypeId;
            existingValue.CreateWhen = registeredValue.CreateWhen;

            await _context.SaveChangesAsync();

            return Ok(existingValue);
        }

        // EXCLUIR LANÇAMENTO
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegisteredValue(int id)
        {
            var registeredValue = await _context.RegisteredValues
                .FindAsync(id);

            if (registeredValue == null)
                return NotFound("Registro não encontrado.");

            _context.RegisteredValues.Remove(registeredValue);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
