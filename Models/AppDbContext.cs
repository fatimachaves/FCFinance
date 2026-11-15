using Microsoft.EntityFrameworkCore;

namespace FCFinance.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {

        }

        public DbSet<Category> Categories => Set<Category>();

        public DbSet<CategoryType> CategoriesType => Set<CategoryType>();

        public DbSet<RegisteredValue> RegisteredValues => Set<RegisteredValue>();

        public DbSet<ValueType> ValueTypes => Set<ValueType>();
        public DbSet<Goal> Goals => Set<Goal>();
    }
}
