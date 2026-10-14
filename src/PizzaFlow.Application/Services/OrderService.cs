using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;
using PizzaFlow.Domain.ValueObjects;

namespace PizzaFlow.Application.Services;

public class OrderService(IStoreRepository stores, IOrderRepository orders, ICurrentUser currentUser,
    OrderLifecycleService lifecycle, IOrderNotifier notifier, IUnitOfWork uow)
{
    // ================= PÚBLICO (consulta pelo cliente e pelo chatbot) =================
    public async Task<PublicOrderDto> GetPublicByCodeAsync(string slug, string code, CancellationToken ct)
    {
        var store = await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.");
        var order = await orders.GetByCodeAsync(store.Id, code.Trim().ToUpperInvariant(), ct)
                    ?? throw new NotFoundException("Pedido não encontrado.");
        return order.ToPublicDto();
    }

    public async Task<IReadOnlyList<PublicOrderDto>> LookupByCpfAsync(string slug, string cpf, CancellationToken ct)
    {
        var digits = Cpf.Normalize(cpf);
        if (!Cpf.IsValid(digits)) throw new DomainException("CPF inválido.");
        var store = await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.");
        var list = await orders.ListByCpfAsync(store.Id, digits, 20, ct);
        return list.Select(o => o.ToPublicDto()).ToList();
    }

    // ================= ADMIN =================
    public async Task<PagedResult<AdminOrderListItemDto>> ListAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var r = await orders.ListAsync(currentUser.StoreId, status, page, pageSize, ct);
        return new PagedResult<AdminOrderListItemDto>(r.Items.Select(o => o.ToListItem()).ToList(), r.Page, r.PageSize, r.TotalCount);
    }

    public async Task<AdminOrderDetailDto> GetAsync(Guid id, CancellationToken ct) =>
        (await Load(id, ct)).ToAdminDetail();

    /// <summary>Pedidos pagos ainda não visualizados pelo admin (alimenta o sino ao abrir a página).</summary>
    public async Task<IReadOnlyList<AdminOrderListItemDto>> ListUnseenAsync(CancellationToken ct) =>
        (await orders.ListUnseenAsync(currentUser.StoreId, ct)).Select(o => o.ToListItem()).ToList();

    public async Task MarkSeenAsync(Guid id, CancellationToken ct)
    {
        var order = await Load(id, ct);
        if (order.AdminSeenAt is not null) return;
        order.AdminSeenAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);
    }

    public async Task<AdminOrderDetailDto> ChangeStatusAsync(Guid id, OrderStatus next, CancellationToken ct)
    {
        var order = await Load(id, ct);
        if (next == OrderStatus.PendingPayment) throw new DomainException("Status inválido.");

        if (next == OrderStatus.Cancelled) await lifecycle.CancelAsync(order, currentUser.UserId, ct);
        else order.ChangeStatus(next, currentUser.UserId);

        await uow.SaveChangesAsync(ct);
        await notifier.OrderStatusChangedAsync(order.StoreId, order.Id, order.Code, order.Status, ct);
        return order.ToAdminDetail();
    }

    private async Task<Order> Load(Guid id, CancellationToken ct) =>
        await orders.GetByIdAsync(currentUser.StoreId, id, ct) ?? throw new NotFoundException("Pedido não encontrado.");
}
