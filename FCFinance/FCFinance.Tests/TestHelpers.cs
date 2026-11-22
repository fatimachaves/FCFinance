using System.Security.Claims;
using FCFinance.Auth;
using FCFinance.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Tests
{
    // Banco SQLite em memória, criado do zero para cada teste (não toca no SQL Server).
    public sealed class TestDb : IDisposable
    {
        private readonly SqliteConnection _connection;

        public TestDb()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = NewContext();
            db.Database.EnsureCreated(); // já cria os tipos 1 = Receita e 2 = Despesa (HasData)
        }

        // Sem userId: contexto "do sistema", que enxerga tudo (para preparar e conferir dados nos testes).
        // Com userId: igual a uma requisição daquele usuário (os filtros globais valem).
        public AppDbContext NewContext(string? userId = null) =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new TestUser(userId));

        private sealed class TestUser(string? userId) : ICurrentUser
        {
            public string? UserId => userId;
            public bool IsSystem => userId == null;
        }

        public void Dispose() => _connection.Dispose();
    }

    public static class Users
    {
        public const string Ana = "user-ana";
        public const string Bia = "user-bia";
        public const string Admin = "user-admin";

        // Simula o usuário logado (mesmos claims que o token JWT da API).
        public static T As<T>(this T controller, string userId, bool admin = false) where T : ControllerBase
        {
            var claims = new List<Claim> { new("sub", userId), new("name", userId) };
            if (admin) claims.Add(new Claim("role", Perfis.Admin));
            var identity = new ClaimsIdentity(claims, "Test", "name", "role");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
            return controller;
        }
    }
}
