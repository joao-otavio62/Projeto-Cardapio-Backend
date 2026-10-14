using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Entities;

namespace PizzaFlow.Infrastructure.Persistence.Seed;

public static class DbSeeder
{
    /// <summary>Cria loja demo + admin inicial (credenciais vindas do .env) apenas se o banco estiver vazio.</summary>
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var cfg = services.GetRequiredService<IConfiguration>();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        if (await db.Stores.AnyAsync(ct)) return;

        var email = cfg["Seed:AdminEmail"];
        var password = cfg["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 10)
        {
            log.LogWarning("Seed ignorado: defina Seed__AdminEmail e Seed__AdminPassword (mín. 10 caracteres).");
            return;
        }

        var store = new Store
        {
            Name = cfg["Seed:StoreName"] ?? "Pizzaria Demo",
            Slug = (cfg["Seed:StoreSlug"] ?? "pizzaria-demo").ToLowerInvariant(),
            DeliveryFee = 6m, MinOrder = 20m, IsOpen = true
        };
        db.Stores.Add(store);

        var hasher = services.GetRequiredService<IPasswordHasher>();
        db.AdminUsers.Add(new AdminUser
        {
            StoreId = store.Id, Name = "Administrador", Email = email.Trim().ToLowerInvariant(), PasswordHash = hasher.Hash(password)
        });

        var pizzas = new Category { StoreId = store.Id, Name = "Pizzas", SortOrder = 1 };
        var drinks = new Category { StoreId = store.Id, Name = "Bebidas", SortOrder = 2 };
        db.Categories.AddRange(pizzas, drinks);
        db.Products.AddRange(
            new Product { StoreId = store.Id, CategoryId = pizzas.Id, Name = "Pizza Calabresa", Description = "Calabresa, cebola e azeitonas.", Price = 49.90m },
            new Product { StoreId = store.Id, CategoryId = pizzas.Id, Name = "Pizza Margherita", Description = "Molho de tomate, muçarela e manjericão.", Price = 46.90m },
            new Product { StoreId = store.Id, CategoryId = pizzas.Id, Name = "Pizza Frango com Catupiry", Description = "Frango desfiado e catupiry.", Price = 52.90m },
            new Product { StoreId = store.Id, CategoryId = drinks.Id, Name = "Refrigerante 2L", Price = 12m, TrackStock = true, StockQuantity = 40, LowStockAlert = 8 });

        await db.SaveChangesAsync(ct);
        log.LogInformation("Seed concluído. Loja: /{Slug} | Admin: {Email}", store.Slug, email);
    }
}
