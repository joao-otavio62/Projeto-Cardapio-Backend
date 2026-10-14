using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class PromotionService(IPromotionRepository promotions, ICurrentUser user, IUnitOfWork uow)
{
    public async Task<IReadOnlyList<PromotionDto>> ListAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return (await promotions.ListAsync(user.StoreId, ct)).OrderByDescending(p => p.StartsAt).Select(p => p.ToDto(now)).ToList();
    }

    public async Task<PromotionDto> CreateAsync(PromotionRequest r, CancellationToken ct)
    {
        var p = new Promotion { StoreId = user.StoreId };
        Apply(p, r);
        promotions.Add(p);
        await uow.SaveChangesAsync(ct);
        return p.ToDto(DateTime.UtcNow);
    }

    public async Task<PromotionDto> UpdateAsync(Guid id, PromotionRequest r, CancellationToken ct)
    {
        var p = await promotions.GetAsync(user.StoreId, id, ct) ?? throw new NotFoundException("Promoção não encontrada.");
        Apply(p, r);
        await uow.SaveChangesAsync(ct);
        return p.ToDto(DateTime.UtcNow);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var p = await promotions.GetAsync(user.StoreId, id, ct) ?? throw new NotFoundException("Promoção não encontrada.");
        promotions.Remove(p); // produtos ficam com promotion_id = NULL (ON DELETE SET NULL)
        await uow.SaveChangesAsync(ct);
    }

    private static void Apply(Promotion p, PromotionRequest r)
    {
        p.Title = r.Title.Trim();
        p.DiscountType = r.DiscountType;
        p.DiscountValue = r.DiscountValue;
        p.StartsAt = r.StartsAt.ToUniversalTime();
        p.EndsAt = r.EndsAt.ToUniversalTime();
        p.IsActive = r.IsActive;
    }
}
