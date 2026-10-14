using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.Abstractions.Repositories;

public interface IUnitOfWork
{
    /// <summary>Persiste tudo atomicamente. Lança ConflictException em concorrência otimista / duplicidade.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IStoreRepository
{
    Task<Store?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Store?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IAdminUserRepository
{
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken ct = default);
}

public interface ICategoryRepository
{
    Task<List<Category>> ListAsync(Guid storeId, CancellationToken ct = default);
    Task<Category?> GetAsync(Guid storeId, Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid storeId, string name, Guid? exceptId, CancellationToken ct = default);
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default);
    void Add(Category category);
    void Remove(Category category);
}

public interface IPromotionRepository
{
    Task<List<Promotion>> ListAsync(Guid storeId, CancellationToken ct = default);
    Task<Promotion?> GetAsync(Guid storeId, Guid id, CancellationToken ct = default);
    void Add(Promotion promotion);
    void Remove(Promotion promotion);
}

public interface IProductRepository
{
    /// <summary>Com mídia, promoção e categoria (tracking).</summary>
    Task<Product?> GetAsync(Guid storeId, Guid id, CancellationToken ct = default);
    /// <summary>Com promoção (tracking) — usado no checkout e na devolução de estoque.</summary>
    Task<List<Product>> GetByIdsAsync(Guid storeId, IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<PagedResult<Product>> ListAsync(Guid storeId, string? normalizedSearch, Guid? categoryId, int page, int pageSize, CancellationToken ct = default);
    /// <summary>Somente ativos, para o cardápio público. A busca recebe o termo JÁ normalizado.</summary>
    Task<List<Product>> SearchMenuAsync(Guid storeId, string? normalizedSearch, Guid? categoryId, CancellationToken ct = default);
    void Add(Product product);
    void Remove(Product product);
    void AddMedia(ProductMedia media);
    void RemoveMedia(ProductMedia media);
    void AddStockMovement(StockMovement movement);
}

public interface ICustomerRepository
{
    Task<Customer?> GetByCpfAsync(Guid storeId, string cpf, CancellationToken ct = default);
    void Add(Customer customer);
}

public interface IOrderRepository
{
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    /// <summary>Tracking, com itens/histórico/pagamentos.</summary>
    Task<Order?> GetByIdAsync(Guid storeId, Guid id, CancellationToken ct = default);
    /// <summary>Somente leitura, com itens/histórico/pagamentos.</summary>
    Task<Order?> GetByCodeAsync(Guid storeId, string code, CancellationToken ct = default);
    Task<List<Order>> ListByCpfAsync(Guid storeId, string cpf, int take, CancellationToken ct = default);
    /// <summary>status == null lista todos EXCETO aguardando pagamento.</summary>
    Task<PagedResult<Order>> ListAsync(Guid storeId, OrderStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<List<Order>> ListUnseenAsync(Guid storeId, CancellationToken ct = default);
    void Add(Order order);
}

public interface IPaymentRepository
{
    /// <summary>Tracking, incluindo Order (com itens e histórico).</summary>
    Task<Payment?> GetByExternalIdAsync(string externalId, CancellationToken ct = default);
    void Add(Payment payment);
}

public interface IReportRepository
{
    Task<ReportSummaryDto> GetSummaryAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, TimeZoneInfo tz, CancellationToken ct = default);
}
