using Microsoft.EntityFrameworkCore;

namespace DaprShop.Products;

public class ProductsDbContext : DbContext
{
    public ProductsDbContext(DbContextOptions<ProductsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<Product>()
            .Property(e => e.Price)
            .HasColumnType("decimal(6,2)");
    }
}
