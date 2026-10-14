using Microsoft.EntityFrameworkCore;
using Npgsql;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Ids são gerados no C# (Guid.NewGuid). Sem isso o EF trataria entidades novas adicionadas
        // via navegação (ex.: order.History.Add) como já existentes e geraria UPDATE em vez de INSERT.
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => typeof(BaseEntity).IsAssignableFrom(e.ClrType)))
            modelBuilder.Entity(entity.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var e in ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Modified))
            e.Entity.UpdatedAt = now;

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Os dados foram alterados por outra operação. Atualize e tente novamente.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException("Já existe um registro com esses dados.");
        }
    }
}
