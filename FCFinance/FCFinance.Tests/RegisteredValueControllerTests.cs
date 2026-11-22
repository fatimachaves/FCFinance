using FCFinance.Controllers;
using FCFinance.DTO;
using FCFinance.Models;
using FCFinance.Services;
using Microsoft.AspNetCore.Mvc;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Tests
{
    public class RegisteredValueControllerTests
    {
        // Controller como numa requisição do usuário: contexto com os filtros globais desse usuário + login simulado.
        private static RegisteredValueController NewController(TestDb db, string userId)
        {
            var ctx = db.NewContext(userId);
            return new RegisteredValueController(ctx, new RegisteredValueService(ctx)).As(userId);
        }

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

            var result = await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa, 62.07m));

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

            var result = await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Receita));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Recusa_categoria_de_outro_usuario()
        {
            using var db = new TestDb();
            var (_, _, despesaDaBia) = Seed(db);

            var result = await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesaDaBia, ValueType.Despesa));

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Listagem_traz_so_os_lancamentos_do_usuario_e_respeita_filtros()
        {
            using var db = new TestDb();
            var (despesa, receita, _) = Seed(db);
            await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));
            await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(receita, ValueType.Receita));
            await NewController(db, Users.Bia).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));

            var controller = NewController(db, Users.Ana);
            var todos = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(null, null, null, null, null, null));
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(todos.Value).Count());

            var soDespesas = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(null, null, null, ValueType.Despesa, null, null));
            Assert.Single(Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(soDespesas.Value));

            var foraDoPeriodo = Assert.IsType<OkObjectResult>(await controller.ListRegisteredValue(new DateTime(2026, 12, 1), null, null, null, null, null));
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<RegisteredValueResponseDTO>>(foraDoPeriodo.Value));
        }

        // ---------- Filtros globais e dados do usuário (LGPD) ----------

        [Fact]
        public async Task Filtro_global_esconde_dados_de_outro_usuario_mesmo_sem_where_no_codigo()
        {
            using var db = new TestDb();
            var (despesa, _, _) = Seed(db);
            await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));
            await NewController(db, Users.Bia).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));

            // Consulta "esquecida", sem nenhum filtro por usuário: o banco filtra sozinho.
            using var comoAna = db.NewContext(Users.Ana);
            Assert.All(comoAna.RegisteredValues.ToList(), v => Assert.Equal(Users.Ana, v.UserId));
            Assert.Single(comoAna.RegisteredValues.ToList());

            // A categoria "Pet" é da Bia: a Ana vê só as do sistema.
            Assert.DoesNotContain(comoAna.Categories.ToList(), c => c.UserId == Users.Bia);
        }

        [Fact]
        public async Task Excluir_dados_apaga_tudo_do_usuario_e_nada_dos_outros()
        {
            using var db = new TestDb();
            var (despesa, _, despesaDaBia) = Seed(db);
            await NewController(db, Users.Bia).SetRegisteredValue(Lancamento(despesaDaBia, ValueType.Despesa));
            await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa));

            // O admin apaga a Bia: o serviço precisa enxergar os dados dela (IgnoreQueryFilters).
            await new UserDataService(db.NewContext(Users.Admin)).DeleteAllAsync(Users.Bia);

            using var check = db.NewContext();
            Assert.DoesNotContain(check.RegisteredValues.ToList(), v => v.UserId == Users.Bia);
            Assert.DoesNotContain(check.Categories.ToList(), c => c.UserId == Users.Bia);
            Assert.Single(check.RegisteredValues.ToList(), v => v.UserId == Users.Ana);   // a Ana não foi afetada
            Assert.Contains(check.Categories.ToList(), c => c.Id == despesa);              // categoria do sistema continua
        }

        [Fact]
        public async Task Exportacao_traz_so_os_dados_do_proprio_usuario()
        {
            using var db = new TestDb();
            var (despesa, _, despesaDaBia) = Seed(db);
            await NewController(db, Users.Ana).SetRegisteredValue(Lancamento(despesa, ValueType.Despesa, 10));
            await NewController(db, Users.Bia).SetRegisteredValue(Lancamento(despesaDaBia, ValueType.Despesa, 99));

            var export = await new UserDataService(db.NewContext(Users.Ana)).ExportAsync();
            var json = System.Text.Json.JsonSerializer.Serialize(export);

            Assert.Contains("\"Valor\":10", json);
            Assert.DoesNotContain("99", json);   // valor da Bia
            Assert.DoesNotContain("Pet", json);  // categoria da Bia
        }
    }
}
