using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Infrastructure.Realtime;

/// <summary>
/// Hub do sino do dashboard. Eventos enviados ao cliente:
///  - "NewOrder"           -> NewOrderNotification
///  - "OrderStatusChanged" -> { orderId, code, status, statusLabel }
/// Cada admin entra automaticamente no grupo da PRÓPRIA loja (claim store_id do JWT).
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminNotificationsHub : Hub
{
    public static string GroupFor(Guid storeId) => $"store-{storeId}";

    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.User?.FindFirst("store_id")?.Value, out var storeId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(storeId));
        await base.OnConnectedAsync();
    }
}

public class SignalROrderNotifier(IHubContext<AdminNotificationsHub> hub, ILogger<SignalROrderNotifier> logger) : IOrderNotifier
{
    public Task NewOrderAsync(Guid storeId, NewOrderNotification n, CancellationToken ct = default)
    {
        logger.LogInformation("Novo pedido {Code} notificado à loja {Store}", n.Code, storeId);
        return hub.Clients.Group(AdminNotificationsHub.GroupFor(storeId)).SendAsync("NewOrder", n, ct);
    }

    public Task OrderStatusChangedAsync(Guid storeId, Guid orderId, string code, OrderStatus status, CancellationToken ct = default) =>
        hub.Clients.Group(AdminNotificationsHub.GroupFor(storeId))
            .SendAsync("OrderStatusChanged", new { orderId, code, status = status.ToString(), statusLabel = Application.Common.OrderStatusLabels.Get(status) }, ct);
}
