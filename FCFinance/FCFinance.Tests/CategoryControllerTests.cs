using FCFinance.Controllers;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using ValueType = FCFinance.Models.ValueType;

namespace FCFinance.Tests
{
    public class CategoryControllerTests
    {
        private static int SeedSystemCategory(TestDb db)
        {
            using var ctx = db.NewContext();
            var c = new Category { Name = "Mercado", TypeId = ValueType.Despesa };
            ctx.Categories.Add(c);
            ctx.SaveChanges();
            return c.Id;
        }

        private static IEnumerable<CategoryResponseDTO> Items(IActionResult result) =>
            Assert.IsAssignableFrom<IEnumerable<CategoryResponseDTO>>(Assert.IsType<OkObjectResult>(result).Value);

        [Fact]
        public async Task Usuario_ve_categorias_do_sistema_e_as_proprias_mas_nao_as_de_outros()
        {
            using var db = new TestDb();
            SeedSystemCategory(db);
            await new CategoryController(db.NewContext(Users.Ana)).As(Users.Ana).SetCategory(new CategoryDTO { Name = "Da Ana", CategoryTypeId = ValueType.Despesa });
            await new CategoryController(db.NewContext(Users.Bia)).As(Users.Bia).SetCategory(new CategoryDTO { Name = "Da Bia", CategoryTypeId = ValueType.Despesa });

            var lista = Items(await new CategoryController(db.NewContext(Users.Ana)).As(Users.Ana).ListCategories()).ToList();

            Assert.Equal(new[] { "Mercado", "Da Ana" }, lista.Select(c => c.Name));
            Assert.True(lista[0].IsSystem);
            Assert.False(lista[0].CanEdit);
            Assert.True(lista[1].CanEdit);
        }

        [Fact]
        public async Task Usuario_comum_nao_altera_categoria_do_sistema_e_admin_altera()
        {
            using var db = new TestDb();
            var id = SeedSystemCategory(db);
            var dto = new CategoryDTO { Name = "Supermercado", CategoryTypeId = ValueType.Despesa };

            var comoAna = await new CategoryController(db.NewContext(Users.Ana)).As(Users.Ana).UpdateCategory(id, dto);
            Assert.Equal(403, Assert.IsType<ObjectResult>(comoAna).StatusCode);

            var comoAdmin = await new CategoryController(db.NewContext(Users.Admin)).As(Users.Admin, admin: true).UpdateCategory(id, dto);
            Assert.IsType<OkObjectResult>(comoAdmin);
        }

        [Fact]
        public async Task Recusa_tipo_de_categoria_inexistente()
        {
            using var db = new TestDb();

            var result = await new CategoryController(db.NewContext(Users.Ana)).As(Users.Ana)
                .SetCategory(new CategoryDTO { Name = "X", CategoryTypeId = 99 });

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
