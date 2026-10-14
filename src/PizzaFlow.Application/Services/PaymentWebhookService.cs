using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class PaymentWebhookService(IPaymentGateway gateway, IPaymentRepository payments, OrderLifecycleService lifecycle,
    IOrderNotifier notifier, IUnitOfWork uow, ILogger<PaymentWebhookService> logger)
{
    /// <summary>
    /// Idempotente. Valida assinatura, reconsulta o pagamento no gateway (fonte da verdade) e sincroniza o pedido.
    /// Lança NotFoundException se o pagamento ainda não foi gravado (o gateway reenvia o webhook depois).
    /// </summary>
    public async Task ProcessAsync(string dataId, string? signature, string? requestId, CancellationToken ct)
    {
        if (!gateway.ValidateWebhookSignature(dataId, signature, requestId))
            throw new AuthenticationFailedException("Assinatura do webhook inválida.");

        var payment = await payments.GetByExternalIdAsync(dataId, ct)
                      ?? throw new NotFoundException("Pagamento não encontrado.");
        var order = payment.Order ?? throw new InvalidOperationException("Pedido do pagamento não carregado.");

        var current = await gateway.GetPaymentAsync(dataId, ct);
        payment.RawWebhook = current.RawJson ?? payment.RawWebhook;

        var becamePaid = await lifecycle.ApplyPaymentStatusAsync(order, payment, current.Status, ct);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (ConflictException)
        {
            // outro webhook idêntico venceu a corrida; nada a fazer
            logger.LogInformation("Webhook duplicado concorrente para pagamento {Id}", dataId);
            return;
        }

        if (becamePaid) await lifecycle.NotifyNewOrderAsync(order, ct);
        else await notifier.OrderStatusChangedAsync(order.StoreId, order.Id, order.Code, order.Status, ct);
    }

}
