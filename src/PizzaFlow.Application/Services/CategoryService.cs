using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class CategoryService(ICategoryRepository categories, ICurrentUser user, IUnitOfWork uow)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct) =>
        (await categories.ListAsync(user.StoreId, ct)).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).Select(c => c.ToDto()).ToList();

    public async Task<CategoryDto> CreateAsync(CategoryRequest r, CancellationToken ct)
    {
        var name = r.Name.Trim();
        if (await categories.NameExistsAsync(user.StoreId, name, null, ct))
            throw new ConflictException("Já existe uma categoria com esse nome.");
        var c = new Category { StoreId = user.StoreId, Name = name, SortOrder = r.SortOrder, IsActive = r.IsActive };
        categories.Add(c);
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryRequest r, CancellationToken ct)
    {
        var c = await categories.GetAsync(user.StoreId, id, ct) ?? throw new NotFoundException("Categoria não encontrada.");
        var name = r.Name.Trim();
        if (await categories.NameExistsAsync(user.StoreId, name, id, ct))
            throw new ConflictException("Já existe uma categoria com esse nome.");
        c.Name = name; c.SortOrder = r.SortOrder; c.IsActive = r.IsActive;
        await uow.SaveChangesAsync(ct);
        return c.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var c = await categories.GetAsync(user.StoreId, id, ct) ?? throw new NotFoundException("Categoria não encontrada.");
        if (await categories.HasProductsAsync(id, ct))
            throw new ConflictException("A categoria possui produtos. Mova ou exclua os produtos antes.");
        categories.Remove(c);
        await uow.SaveChangesAsync(ct);
    }
}
