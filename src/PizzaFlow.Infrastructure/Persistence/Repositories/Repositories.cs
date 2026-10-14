using Microsoft.EntityFrameworkCore;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Common;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Infrastructure.Persistence.Repositories;

public class StoreRepository(AppDbContext db) : IStoreRepository
{
    public Task<Store?> GetBySlugAsync(string slug, CancellationToken ct) =>
        db.Stores.FirstOrDefaultAsync(s => s.Slug == slug.ToLower(), ct);

    public Task<Store?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);
}

public class AdminUserRepository(AppDbContext db) : IAdminUserRepository
{
    public Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.AdminUsers.FirstOrDefaultAsync(u => u.Email == email, ct);
}

public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<List<Category>> ListAsync(Guid storeId, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(c => c.StoreId == storeId).ToListAsync(ct);

    public Task<Category?> GetAsync(Guid storeId, Guid id, CancellationToken ct) =>
        db.Categories.FirstOrDefaultAsync(c => c.StoreId == storeId && c.Id == id, ct);

    public Task<bool> NameExistsAsync(Guid storeId, string name, Guid? exceptId, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.StoreId == storeId && c.Name.ToLower() == name.ToLower() && (exceptId == null || c.Id != exceptId), ct);

    public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct) =>
        db.Products.AnyAsync(p => p.CategoryId == categoryId, ct);

    public void Add(Category category) => db.Categories.Add(category);
    public void Remove(Category category) => db.Categories.Remove(category);
}

public class PromotionRepository(AppDbContext db) : IPromotionRepository
{
    public Task<List<Promotion>> ListAsync(Guid storeId, CancellationToken ct) =>
        db.Promotions.AsNoTracking().Where(p => p.StoreId == storeId).ToListAsync(ct);

    public Task<Promotion?> GetAsync(Guid storeId, Guid id, CancellationToken ct) =>
        db.Promotions.FirstOrDefaultAsync(p => p.StoreId == storeId && p.Id == id, ct);

    public void Add(Promotion promotion) => db.Promotions.Add(promotion);
    public void Remove(Promotion promotion) => db.Promotions.Remove(promotion);
}

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(Guid storeId, Guid id, CancellationToken ct) =>
        db.Products.Include(p => p.Media).Include(p => p.Promotion).Include(p => p.Category)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.StoreId == storeId && p.Id == id, ct);

    public Task<List<Product>> GetByIdsAsync(Guid storeId, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return db.Products.Include(p => p.Promotion).Where(p => p.StoreId == storeId && list.Contains(p.Id)).ToListAsync(ct);
    }

    public async Task<PagedResult<Product>> ListAsync(Guid storeId, string? term, Guid? categoryId, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().Where(p => p.StoreId == storeId);
        if (categoryId is not null) q = q.Where(p => p.CategoryId == categoryId);
        q = ApplySearch(q, term);

        var total = await q.CountAsync(ct);
        var items = await q.Include(p => p.Media).Include(p => p.Promotion).Include(p => p.Category)
            .OrderBy(p => p.Name).Skip((page - 1) * pageSize).Take(pageSize).AsSplitQuery().ToListAsync(ct);
        return new PagedResult<Product>(items, page, pageSize, total);
    }

    public Task<List<Product>> SearchMenuAsync(Guid storeId, string? term, Guid? categoryId, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().Where(p => p.StoreId == storeId && p.IsActive);
        if (categoryId is not null) q = q.Where(p => p.CategoryId == categoryId);
        q = ApplySearch(q, term);
        return q.Include(p => p.Media).Include(p => p.Promotion).AsSplitQuery().ToListAsync(ct);
    }

    /// <summary>Busca "fuzzy": contém o termo OU é parecido por trigramas (tolera erros de digitação). Termo já normalizado.</summary>
    private static IQueryable<Product> ApplySearch(IQueryable<Product> q, string? term)
    {
        if (string.IsNullOrWhiteSpace(term)) return q;
        var pattern = "%" + term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return q.Where(p => EF.Functions.Like(p.NameSearch, pattern) || EF.Functions.TrigramsSimilarity(p.NameSearch, term) > 0.3f);
    }

    public void Add(Product product) => db.Products.Add(product);
    public void Remove(Product product) => db.Products.Remove(product);
    public void AddMedia(ProductMedia media) => db.ProductMedia.Add(media);
    public void RemoveMedia(ProductMedia media) => db.ProductMedia.Remove(media);
    public void AddStockMovement(StockMovement movement) => db.StockMovements.Add(movement);
}

public class CustomerRepository(AppDbContext db) : ICustomerRepository
{
    public Task<Customer?> GetByCpfAsync(Guid storeId, string cpf, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.StoreId == storeId && c.Cpf == cpf, ct);

    public void Add(Customer customer) => db.Customers.Add(customer);
}

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => db.Orders.AnyAsync(o => o.Code == code, ct);

    public Task<Order?> GetByIdAsync(Guid storeId, Guid id, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).Include(o => o.History).Include(o => o.Payments)
            .AsSplitQuery().FirstOrDefaultAsync(o => o.StoreId == storeId && o.Id == id, ct);

    public Task<Order?> GetByCodeAsync(Guid storeId, string code, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).Include(o => o.Payments)
            .AsSplitQuery().FirstOrDefaultAsync(o => o.StoreId == storeId && o.Code == code, ct);

    public Task<List<Order>> ListByCpfAsync(Guid storeId, string cpf, int take, CancellationToken ct) =>
        db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).Include(o => o.Payments)
            .Where(o => o.StoreId == storeId && o.CustomerCpf == cpf)
            .OrderByDescending(o => o.CreatedAt).Take(take).AsSplitQuery().ToListAsync(ct);

    public async Task<PagedResult<Order>> ListAsync(Guid storeId, OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Orders.AsNoTracking().Where(o => o.StoreId == storeId);
        q = status is null ? q.Where(o => o.Status != OrderStatus.PendingPayment) : q.Where(o => o.Status == status);
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<Order>(items, page, pageSize, total);
    }

    public Task<List<Order>> ListUnseenAsync(Guid storeId, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Where(o => o.StoreId == storeId && o.AdminSeenAt == null
                        && o.Status != OrderStatus.PendingPayment && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.CreatedAt).Take(50).ToListAsync(ct);

    public void Add(Order order) => db.Orders.Add(order);
}

public class PaymentRepository(AppDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByExternalIdAsync(string externalId, CancellationToken ct) =>
        db.Payments
            .Include(p => p.Order).ThenInclude(o => o!.Items)
            .Include(p => p.Order).ThenInclude(o => o!.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ExternalId == externalId, ct);

    public void Add(Payment payment) => db.Payments.Add(payment);
}
