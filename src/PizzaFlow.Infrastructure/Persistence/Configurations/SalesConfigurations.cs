using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PizzaFlow.Domain.Entities;

namespace PizzaFlow.Infrastructure.Persistence.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers", t => t.HasCheckConstraint("ck_customers_cpf", "cpf ~ '^[0-9]{11}$'"));
        b.Property(x => x.FullName).HasMaxLength(160).IsRequired();
        b.Property(x => x.Cpf).HasColumnType("char(11)").IsRequired();
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(160);
        b.HasIndex(x => new { x.StoreId, x.Cpf }).IsUnique();
        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders", t =>
        {
            t.HasCheckConstraint("ck_orders_total", "total >= 0 AND subtotal >= 0");
            t.HasCheckConstraint("ck_orders_delivery_address",
                "delivery_type = 'Pickup' OR (addr_street IS NOT NULL AND addr_number IS NOT NULL AND addr_city IS NOT NULL)");
        });
        b.Property(x => x.Code).HasMaxLength(12).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DeliveryType).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.CustomerName).HasMaxLength(160).IsRequired();
        b.Property(x => x.CustomerCpf).HasColumnType("char(11)").IsRequired();
        b.Property(x => x.CustomerPhone).HasMaxLength(20);
        b.Property(x => x.AddrZip).HasMaxLength(9);
        b.Property(x => x.AddrStreet).HasMaxLength(160);
        b.Property(x => x.AddrNumber).HasMaxLength(20);
        b.Property(x => x.AddrComplement).HasMaxLength(80);
        b.Property(x => x.AddrDistrict).HasMaxLength(80);
        b.Property(x => x.AddrCity).HasMaxLength(80);
        b.Property(x => x.AddrState).HasColumnType("char(2)");
        b.Property(x => x.Subtotal).HasPrecision(12, 2);
        b.Property(x => x.DiscountTotal).HasPrecision(12, 2);
        b.Property(x => x.DeliveryFee).HasPrecision(12, 2);
        b.Property(x => x.Total).HasPrecision(12, 2);
        b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => new { x.StoreId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.StoreId, x.CustomerCpf });
        b.HasIndex(x => x.CustomerId);

        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(h => h.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.ToTable("order_items", t => t.HasCheckConstraint("ck_order_items_qty", "quantity > 0"));
        b.Property(x => x.ProductName).HasMaxLength(140).IsRequired();
        b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        b.Property(x => x.Discount).HasPrecision(12, 2);
        b.Property(x => x.Total).HasPrecision(12, 2);
        b.Property(x => x.Notes).HasMaxLength(255);
        // snapshot: apagar o produto não apaga o histórico de vendas
        b.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.ToTable("payments", t => t.HasCheckConstraint("ck_payments_amount", "amount > 0"));
        b.Property(x => x.Provider).HasMaxLength(20).IsRequired();
        b.Property(x => x.Method).HasConversion<string>().HasMaxLength(15);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.ExternalId).HasMaxLength(100);
        b.Property(x => x.CardLast4).HasColumnType("char(4)");
        b.Property(x => x.CardBrand).HasMaxLength(20);
        b.Property(x => x.RawWebhook).HasColumnType("jsonb");
        b.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        b.HasIndex(x => x.ExternalId).IsUnique();   // idempotência
        b.HasIndex(x => x.OrderId);
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> b)
    {
        b.ToTable("order_status_history");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.OrderId, x.CreatedAt });
        b.HasOne<AdminUser>().WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.SetNull);
    }
}
