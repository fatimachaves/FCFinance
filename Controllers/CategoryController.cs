
using FCFinance.DTO;
using FCFinance.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FCFinance.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoryController(AppDbContext appContext)
        {
            _context = appContext;
        }

        // LISTAR CATEGORIAS
        [HttpGet]
        public async Task<IActionResult> ListCategories()
        {
            var categories = await _context.Categories.ToListAsync();

            return Ok(categories);
        }

        // BUSCAR CATEGORIA POR ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
                return NotFound("Categoria não encontrada.");

            return Ok(category);
        }

        // CADASTRAR CATEGORIA
        [HttpPost]
        public async Task<IActionResult> SetCategory(CategoryDTO category)
        {
            var newCategory = new Category()
            {
                Name = category.Name,
                CategoryTypeId = category.CategoryTypeId
            };

            _context.Categories.Add(newCategory);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetCategory),
                new { id = newCategory.Id },
                newCategory
            );
        }

        // ATUALIZAR CATEGORIA
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(
            int id, CategoryDTO category)
        {
            var existingCategory = await _context.Categories.FindAsync(id);

            if (existingCategory == null)
                return NotFound("Categoria não encontrada.");

            existingCategory.Name = category.Name;
            existingCategory.CategoryTypeId = category.CategoryTypeId;

            await _context.SaveChangesAsync();

            return Ok(existingCategory);
        }

        // EXCLUIR CATEGORIA
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            if (category == null)
                return NotFound("Categoria não encontrada.");

            _context.Categories.Remove(category);

            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
