using System.Security.Claims;

namespace FCFinance.Auth
{
    // Quem está fazendo a requisição. O AppDbContext usa isso nos FILTROS GLOBAIS: toda consulta a
    // lançamentos, metas e categorias já vem filtrada pelo usuário logado, mesmo que o código esqueça
    // o .Where(UserId == ...). É a proteção contra um cliente ver os dados de outro.
    public interface ICurrentUser
    {
        string? UserId { get; }

        // true só em tarefas internas/testes que precisam enxergar tudo. Nunca usado em requisições.
        bool IsSystem { get; }
    }

    // Usuário da requisição HTTP (claim "sub" do token). Sem login, UserId é null e os filtros não devolvem nada.
    public class HttpCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

        public string? UserId => _accessor.HttpContext?.User.FindFirstValue("sub");
        public bool IsSystem => false;
    }
}
