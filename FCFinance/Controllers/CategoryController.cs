
using FCFinance.Auth;
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    // Categorias do sistema (UserId = null) aparecem para todos; só o Admin altera.
    // Categorias próprias (UserId = usuário) só aparecem e só são alteradas pelo dono.
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext appContext)
        {
            _context = appContext;
        }

        // Categorias que o usuário logado enxerga: as do sistema + as dele.
        private IQueryable<Category> Visible()
        {
            var userId = User.GetUserId();
            return _context.Categories.Where(c => c.UserId == null || c.UserId == userId);
        }

        private bool CanEdit(Category c) =>
            c.UserId == null ? User.IsAdmin() : c.UserId == User.GetUserId();

        private CategoryResponseDTO ToResponse(Category c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            TypeId = c.TypeId,
            IsSystem = c.UserId == null,
            CanEdit = CanEdit(c)
        };

        // LISTAR CATEGORIAS
        [HttpGet]
        public async Task<IActionResult> ListCategories()
        {
            var categories = await Visible()
                .OrderBy(c => c.UserId != null) // sistema primeiro
                .ThenBy(c => c.Name)
                .ToListAsync();

            return Ok(categories.Select(ToResponse));
        }

        // BUSCAR CATEGORIA POR ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await Visible().SingleOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound("Categoria não encontrada.");

            return Ok(ToResponse(category));
        }

        // CADASTRAR CATEGORIA
        [HttpPost]
        public async Task<IActionResult> SetCategory(CategoryDTO category)
        {
            var error = await ValidateAsync(category);
            if (error != null) return BadRequest(error);

            var newCategory = new Category()
            {
                Name = category.Name.Trim(),
                TypeId = category.CategoryTypeId,
                UserId = category.IsSystem && User.IsAdmin() ? null : User.GetUserId()
            };

            _context.Categories.Add(newCategory);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetCategory),
                new { id = newCategory.Id },
                ToResponse(newCategory)
            );
        }

        // ATUALIZAR CATEGORIA
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(
            int id, CategoryDTO category)
        {
            var existingCategory = await Visible().SingleOrDefaultAsync(c => c.Id == id);

            if (existingCategory == null)
                return NotFound("Categoria não encontrada.");

            if (!CanEdit(existingCategory))
                return StatusCode(StatusCodes.Status403Forbidden, "Categorias do sistema só podem ser alteradas pelo administrador.");

            var error = await ValidateAsync(category);
            if (error != null) return BadRequest(error);

            existingCategory.Name = category.Name.Trim();
            existingCategory.TypeId = category.CategoryTypeId;

            await _context.SaveChangesAsync();

            return Ok(ToResponse(existingCategory));
        }

        // EXCLUIR CATEGORIA
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await Visible().SingleOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound("Categoria não encontrada.");

            if (!CanEdit(category))
                return StatusCode(StatusCodes.Status403Forbidden, "Categorias do sistema só podem ser excluídas pelo administrador.");

            // Não deixa excluir uma categoria que ainda tem lançamentos (de qualquer usuário,
            // no caso das categorias do sistema). O banco recusaria com erro 500.
            var emUso = await _context.RegisteredValues.AnyAsync(x => x.CategoryId == id);
            if (emUso)
                return Conflict("Categoria em uso: mude ou exclua os lançamentos dela antes de excluir.");

            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<string?> ValidateAsync(CategoryDTO category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return "Informe o nome da categoria.";

            if (!await _context.ValueTypes.AnyAsync(t => t.Id == category.CategoryTypeId))
                return "Tipo de categoria inválido.";

            return null;
        }
    }
}
