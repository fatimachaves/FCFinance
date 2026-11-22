using FCFinance.Controllers;
using FCFinance.DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Tests
{
    public class GoalsControllerTests
    {
        private static GoalDTO NewGoal(string name, bool priority = false) =>
            new() { Name = name, TargetValue = 1000, CurrentValue = 100, IsPriority = priority };

        private static async Task<int> CreateAsync(TestDb db, string userId, GoalDTO dto)
        {
            var result = await new GoalsController(db.NewContext(userId)).As(userId).SetGoal(dto);
            var created = Assert.IsType<CreatedAtActionResult>(result);
            return Assert.IsType<GoalResponseDTO>(created.Value).Id;
        }

        [Fact]
        public async Task Definir_prioritaria_tira_a_prioridade_da_anterior()
        {
            using var db = new TestDb();
            var primeira = await CreateAsync(db, Users.Ana, NewGoal("Viagem", priority: true));
            var segunda = await CreateAsync(db, Users.Ana, NewGoal("Reserva"));

            await new GoalsController(db.NewContext(Users.Ana)).As(Users.Ana).SetPriority(segunda);

            using var check = db.NewContext();
            var prioritarias = await check.Goals.Where(g => g.UserId == Users.Ana && g.IsPriority).Select(g => g.Id).ToListAsync();
            Assert.Equal(new[] { segunda }, prioritarias);
            Assert.NotEqual(primeira, segunda);
        }

        [Fact]
        public async Task Prioritaria_de_um_usuario_nao_afeta_a_de_outro()
        {
            using var db = new TestDb();
            await CreateAsync(db, Users.Ana, NewGoal("Meta da Ana", priority: true));
            await CreateAsync(db, Users.Bia, NewGoal("Meta da Bia", priority: true));

            using var check = db.NewContext();
            Assert.Equal(2, await check.Goals.CountAsync(g => g.IsPriority));
        }

        [Fact]
        public async Task Usuario_nao_ve_nem_altera_meta_de_outro()
        {
            using var db = new TestDb();
            var metaDaAna = await CreateAsync(db, Users.Ana, NewGoal("Meta da Ana"));

            var comoBia = new GoalsController(db.NewContext(Users.Bia)).As(Users.Bia);
            Assert.IsType<NotFoundObjectResult>(await comoBia.GetGoal(metaDaAna));
            Assert.IsType<NotFoundObjectResult>(await comoBia.DeleteGoal(metaDaAna));

            var lista = Assert.IsType<OkObjectResult>(await comoBia.ListGoals());
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<GoalResponseDTO>>(lista.Value));
        }
    }
}
