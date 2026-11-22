using FCFinance.Models;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Services
{
    // Operações sobre TODOS os dados de um usuário: exportar (LGPD) e apagar (exclusão de conta).
    public class UserDataService
    {
        private readonly AppDbContext _context;

        public UserDataService(AppDbContext context) => _context = context;

        // Apaga lançamentos, metas e categorias próprias do usuário (as do sistema ficam).
        // Usa IgnoreQueryFilters: o admin pode estar apagando os dados de OUTRA pessoa.
        public async Task DeleteAllAsync(string userId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            // Lançamentos antes das categorias, por causa da chave estrangeira.
            await _context.RegisteredValues.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _context.Goals.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _context.Categories.IgnoreQueryFilters().Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }

        // Cópia dos dados financeiros do usuário logado (os filtros globais já limitam ao dono).
        public async Task<object> ExportAsync()
        {
            var categorias = await _context.Categories
                .Where(c => c.UserId != null) // só as criadas pelo usuário; as do sistema são de todos
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, Nome = c.Name, Tipo = c.Type.Name })
                .ToListAsync();

            var lancamentos = await _context.RegisteredValues
                .OrderBy(r => r.CreateWhen).ThenBy(r => r.Id)
                .Select(r => new { r.Id, Data = r.CreateWhen, Descricao = r.Name, Valor = r.Value, Tipo = r.ValueType.Name, Categoria = r.Category.Name })
                .ToListAsync();

            var metas = await _context.Goals
                .OrderBy(g => g.Name)
                .Select(g => new
                {
                    g.Id, Nome = g.Name, Alvo = g.TargetValue, Atual = g.CurrentValue, CriadaEm = g.CreatedWhen,
                    DataLimite = g.EndDate, Mensal = g.IsMonthly, Prioritaria = g.IsPriority
                })
                .ToListAsync();

            return new { categorias, lancamentos, metas };
        }
    }
}
