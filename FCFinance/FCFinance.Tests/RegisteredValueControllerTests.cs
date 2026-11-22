using FCFinance.Controllers;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Tests
{
    public class RegisteredValueControllerTests
    {
        // Cria categorias: uma de despesa do sistema, uma de receita do sistema e uma de despesa da Bia.
        private static (int despesaSistema, int receitaSistema, int despesaDaBia) Seed(TestDb db)
        {
            using var ctx = db.NewContext();
            var despesa = new Category { Name = "Mercado", TypeId = ValueType.Despesa };
            var receita = new Category { Name = "Salário", TypeId = ValueType.Receita };
            var daBia = new Category { Name = "Pet", TypeId = ValueType.Despesa, UserId = Users.Bia };
            ctx.Categories.AddRange(despesa, receita, daBia);
            ctx.SaveChanges();
            return (despesa.Id, receita.Id, daBia.Id);
        }

        private static RegisteredValueDTO Lancamento(int categoryId, int typeId, decimal value = 50) =>
            new() { Name = "Teste", Value = value, CategoryId = categoryId, ValueTypeId = typeId, CreateWhen = new DateTime(2026, 11, 10) };

        [Fact]
        public async Task Cria_lancamento_valido_e_guarda_o_dono()
        {
            using var db = new TestDb();
            var (despesa, _, _) = Seed(db);

            var result = await new RegisteredValueController(db.NewContext()).As(Users.Ana)
                .SetRegisteredValue(Lancamento(despesa, ValueType.Despesa, 62.07m));

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var dto = Assert.IsType<RegisteredValueResponseDTO>(created.Value);
            Assert.Equal(62.07m, dto.Value);
            using var check = db.NewContext();
            Assert.Equal(Users.Ana, check.RegisteredValues.Single().UserId);
        }

        [Fact]
        public async Task Recusa_receita_em_categoria_de_despesa()
        {
            using var db = new TestDb();
            var (despesa, _, _) = Seed(db);

            var result = await new RegisteredValueController(db.NewContext()).As(Users.Ana)
                .SetRegisteredValue(Lancamento(despesa, ValueType.Receita));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Recusa_categoria_de_outro_usuario()
        {
            using var db = new TestDb();
            var (_, _, despesaDaBia) = Seed(db);

            var result = await new RegisteredValueController(db.NewContext()).As(Users.Ana)
                .SetRegisteredValue(Lancamento(despesaDaBia, ValueType.Despesa));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Listagem_traz_so_os_lancamentos_do_usuario_e_respeita_filtros()
        {
            using var db = new TestDb();
            var (despesa, receita, _) = Seed(db);
            await new RegisteredValueController(db.NewContext()).As(Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));
            await new RegisteredValueController(db.NewContext()).As(Users.Ana).SetRegisteredValue(Lancamento(receita, ValueType.Receita));
            await new RegisteredValueController(db.NewContext()).As(Users.Bia).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));

            var controller = new RegisteredValueController(db.NewContext()).As(Users.Ana);
            var todos = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(null, null, null, null, null, null));
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(todos.Value).Count());

            var soDespesas = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(null, null, null, ValueType.Despesa, null, null));
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(soDespesas.Value));

            var foraDoPeriodo = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(new DateTime(2026, 12, 1), null, null, null, null, null));
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(foraDoPeriodo.Value));
        }
    }
}
