using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.Services;

/// <summary>
/// Regras compartilhadas entre checkout, webhook e admin: cancelamento + devolução de estoque,
/// sincronização status do pagamento -> status do pedido, notificação em tempo real.
/// NÃO persiste: quem chama faz o SaveChanges (uma única transação por operação).
/// </summary>
public class OrderLifecycleService(IProductRepository products, IOrderNotifier notifier, ILogger<OrderLifecycleService> logger)
{
    public async Task CancelAsync(Order order, Guid? changedBy, CancellationToken ct)
    {
        order.ChangeStatus(OrderStatus.Cancelled, changedBy);
        await RestoreStockAsync(order, ct);
    }

    public async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        var lines = order.Items.Where(i => i.ProductId is not null)
            .GroupBy(i => i.ProductId!.Value).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        if (lines.Count == 0) return;

        var found = await products.GetByIdsAsync(order.StoreId, lines.Keys, ct);
        foreach (var p in found.Where(p => p.TrackStock))
        {
            var qty = lines[p.Id];
            p.AddStock(qty);
            products.AddStockMovement(new StockMovement
            {
                ProductId = p.Id, OrderId = order.Id, Quantity = qty, Reason = StockReason.Cancellation
            });
        }
    }

    /// <summary>Aplica o status vindo do gateway. Retorna true se o pedido acabou de ser confirmado (pago).</summary>
    public async Task<bool> ApplyPaymentStatusAsync(Order order, Payment payment, PaymentStatus newStatus, CancellationToken ct)
    {
        if (payment.Status == newStatus) return false; // idempotência
        payment.Status = newStatus;
        payment.UpdatedAt = DateTime.UtcNow;

        switch (newStatus)
        {
            case PaymentStatus.Approved:
                payment.PaidAt = DateTime.UtcNow;
                if (order.Status == OrderStatus.PendingPayment)
                {
                    order.ChangeStatus(OrderStatus.Received, null);
                    return true;
                }
                logger.LogWarning("Pagamento {Payment} aprovado, mas pedido {Code} está {Status}. Requer estorno manual.",
                    payment.ExternalId, order.Code, order.Status);
                break;

            case PaymentStatus.Rejected or PaymentStatus.Cancelled or PaymentStatus.Expired:
                if (order.Status == OrderStatus.PendingPayment) await CancelAsync(order, null, ct);
                break;
        }
        return false;
    }

    public async Task NotifyNewOrderAsync(Order order, CancellationToken ct)
    {
        try
        {
            await notifier.NewOrderAsync(order.StoreId,
                new NewOrderNotification(order.Id, order.Code, order.CustomerName, order.Total, order.DeliveryType.ToString(), order.CreatedAt), ct);
        }
        catch (Exception ex)
        {
            // falha de tempo real nunca pode derrubar o pedido já pago
            logger.LogError(ex, "Falha ao notificar novo pedido {Code}", order.Code);
        }
    }
}
