using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

/// <summary>Cardápio público + configurações da loja (admin).</summary>
public class StoreService(IStoreRepository stores, IProductRepository products, ICategoryRepository categories,
    ICurrentUser currentUser, IUnitOfWork uow)
{
    public async Task<StoreInfoDto> GetPublicAsync(string slug, CancellationToken ct) =>
        (await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.")).ToDto();

    public async Task<MenuResponse> GetMenuAsync(string slug, string? search, Guid? categoryId, CancellationToken ct)
    {
        var store = await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.");
        var term = TextNormalizer.Normalize(search);
        if (term.Length > 80) term = term[..80];

        var items = await products.SearchMenuAsync(store.Id, term.Length == 0 ? null : term, categoryId, ct);
        var cats = (await categories.ListAsync(store.Id, ct)).Where(c => c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Name);
        var now = DateTime.UtcNow;

        var menu = cats
            .Select(c => new MenuCategoryDto(c.Id, c.Name,
                items.Where(p => p.CategoryId == c.Id).OrderBy(p => p.Name).Select(p => p.ToPublicDto(now)).ToList()))
            .Where(c => c.Products.Count > 0)
            .ToList();

        return new MenuResponse(store.ToDto(), menu);
    }

    // ---------- admin ----------
    public async Task<StoreInfoDto> GetAdminAsync(CancellationToken ct) =>
        (await stores.GetByIdAsync(currentUser.StoreId, ct) ?? throw new NotFoundException("Loja não encontrada.")).ToDto();

    public async Task<StoreInfoDto> UpdateAsync(UpdateStoreRequest r, CancellationToken ct)
    {
        var store = await stores.GetByIdAsync(currentUser.StoreId, ct) ?? throw new NotFoundException("Loja não encontrada.");
        store.Name = r.Name.Trim();
        store.Phone = r.Phone?.Trim();
        store.DeliveryFee = r.DeliveryFee;
        store.MinOrder = r.MinOrder;
        store.IsOpen = r.IsOpen;
        await uow.SaveChangesAsync(ct);
        return store.ToDto();
    }
}
