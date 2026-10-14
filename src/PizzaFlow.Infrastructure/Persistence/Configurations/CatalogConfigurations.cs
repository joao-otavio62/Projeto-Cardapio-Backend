using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzaFlow.Domain.Entities;

namespace PizzaFlow.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("categories");
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        b.HasIndex(x => new { x.StoreId, x.Name }).IsUnique();
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> b)
    {
        b.ToTable("promotions", t => t.HasCheckConstraint("ck_promotions_period", "ends_at > starts_at"));
        b.Property(x => x.Title).HasMaxLength(120).IsRequired();
        b.Property(x => x.DiscountType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.DiscountValue).HasPrecision(12, 2);
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_price", "price >= 0");
            t.HasCheckConstraint("ck_products_stock", "stock_quantity >= 0");
        });
        b.Property(x => x.Name).HasMaxLength(140).IsRequired();
        b.Property(x => x.NameSearch).HasMaxLength(140).IsRequired();
        b.Property(x => x.Price).HasPrecision(12, 2);
        b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken(); // evita overselling

        // busca fuzzy: LIKE '%termo%' e similaridade por trigramas, ambos sobre name_search
        b.HasIndex(x => x.NameSearch).HasMethod("gin").HasOperators("gin_trgm_ops");
        b.HasIndex(x => new { x.StoreId, x.CategoryId });

        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany(c => c.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Media).WithOne().HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductMediaConfiguration : IEntityTypeConfiguration<ProductMedia>
{
    public void Configure(EntityTypeBuilder<ProductMedia> b)
    {
        b.ToTable("product_media");
        b.Property(x => x.MediaType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Url).IsRequired();
        b.Property(x => x.PublicId).HasMaxLength(200);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements");
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}
