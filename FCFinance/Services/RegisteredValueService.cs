using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.EntityFrameworkCore;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Services
{
    // Regras para gravar lançamentos (receitas/despesas), num lugar só, para que toda forma de
    // cadastro (site, app, importações futuras) siga exatamente as mesmas validações.
    public class RegisteredValueService
    {
        private readonly AppDbContext _context;

        public RegisteredValueService(AppDbContext context) => _context = context;

        // Categorias que o usuário pode usar: as do sistema + as dele (opcionalmente de um tipo só).
        public IQueryable<Category> CategoriesFor(string userId, int? typeId = null)
        {
            var query = _context.Categories.Where(c => c.UserId == null || c.UserId == userId);
            if (typeId.HasValue) query = query.Where(c => c.TypeId == typeId.Value);
            return query;
        }

        // Retorna null se estiver tudo certo, ou a mensagem de erro para o usuário.
        public async Task<string?> ValidateAsync(string userId, RegisteredValueDTO dto)
        {
            if (dto.Value <= 0)
                return "Informe um valor maior que zero.";

            var category = await CategoriesFor(userId)
                .Where(c => c.Id == dto.CategoryId)
                .Select(c => new { c.TypeId })
                .SingleOrDefaultAsync();
            if (category == null)
                return "Categoria inválida.";

            if (!await _context.ValueTypes.AnyAsync(t => t.Id == dto.ValueTypeId))
                return "Tipo de lançamento inválido.";

            if (category.TypeId != dto.ValueTypeId)
                return dto.ValueTypeId == ValueType.Receita
                    ? "Essa categoria é de despesa. Escolha uma categoria de receita."
                    : "Essa categoria é de receita. Escolha uma categoria de despesa.";

            return null;
        }

        // Valida e grava. Retorna o lançamento criado, ou a mensagem de erro.
        public async Task<(RegisteredValue? Value, string? Error)> CreateAsync(string userId, RegisteredValueDTO dto)
        {
            var error = await ValidateAsync(userId, dto);
            if (error != null) return (null, error);

            var entity = new RegisteredValue
            {
                Name = dto.Name.Trim(),
                Value = dto.Value,
                CategoryId = dto.CategoryId,
                ValueTypeId = dto.ValueTypeId,
                CreateWhen = dto.CreateWhen,
                UserId = userId
            };
            _context.RegisteredValues.Add(entity);
            await _context.SaveChangesAsync();
            return (entity, null);
        }
    }
}
