using Microsoft.Extensions.Logging;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;
using PizzaFlow.Domain.ValueObjects;

namespace PizzaFlow.Application.Services;

public class CheckoutService(
    IStoreRepository stores, IProductRepository products, ICustomerRepository customers,
    IOrderRepository orders, IPaymentRepository payments, IPaymentGateway gateway,
    OrderLifecycleService lifecycle, IUnitOfWork uow, ILogger<CheckoutService> logger)
{
    private const string FallbackEmailDomain = "clientes.pizzaflow.invalid";

    public async Task<CheckoutResponse> CheckoutAsync(string slug, CheckoutRequest req, CancellationToken ct)
    {
        var store = await stores.GetBySlugAsync(slug, ct) ?? throw new NotFoundException("Loja não encontrada.");
        if (!store.IsOpen) throw new DomainException("A loja está fechada no momento.");

        var cpf = Cpf.Normalize(req.Cpf);
        if (!Cpf.IsValid(cpf)) throw new DomainException("CPF inválido.");

        // ---- itens: preços SEMPRE vêm do banco, nunca do cliente ----
        var lines = req.Items
            .GroupBy(i => (i.ProductId, Notes: string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim()))
            .Select(g => (g.Key.ProductId, g.Key.Notes, Quantity: g.Sum(x => x.Quantity)))
            .ToList();
        var found = (await products.GetByIdsAsync(store.Id, lines.Select(l => l.ProductId).Distinct(), ct))
            .ToDictionary(p => p.Id);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            StoreId = store.Id, DeliveryType = req.DeliveryType,
            CustomerName = req.FullName.Trim(), CustomerCpf = cpf, CustomerPhone = Cpf.Normalize(req.Phone) is { Length: > 0 } ph ? ph : null,
            Notes = req.Notes?.Trim()
        };
        do { order.Code = OrderCodeGenerator.Generate(); } while (await orders.CodeExistsAsync(order.Code, ct));

        foreach (var l in lines)
        {
            if (!found.TryGetValue(l.ProductId, out var p) || !p.IsActive)
                throw new DomainException("Um dos produtos do carrinho não está mais disponível.");
            var unitDiscount = p.DiscountFor(now);
            order.Items.Add(new OrderItem
            {
                OrderId = order.Id, ProductId = p.Id, ProductName = p.Name, UnitPrice = p.Price, Quantity = l.Quantity,
                Discount = unitDiscount * l.Quantity, Total = (p.Price - unitDiscount) * l.Quantity, Notes = l.Notes
            });
        }

        order.Subtotal = order.Items.Sum(i => i.UnitPrice * i.Quantity);
        order.DiscountTotal = order.Items.Sum(i => i.Discount);
        if (order.Subtotal - order.DiscountTotal < store.MinOrder)
            throw new DomainException("Pedido mínimo de R$ " + store.MinOrder.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + ".");

        if (req.DeliveryType == DeliveryType.Delivery)
        {
            var a = req.Address ?? throw new DomainException("Endereço é obrigatório para entrega.");
            order.DeliveryFee = store.DeliveryFee;
            order.AddrZip = Cpf.Normalize(a.ZipCode); order.AddrStreet = a.Street.Trim(); order.AddrNumber = a.Number.Trim();
            order.AddrComplement = a.Complement?.Trim(); order.AddrDistrict = a.District.Trim();
            order.AddrCity = a.City.Trim(); order.AddrState = a.State.Trim().ToUpperInvariant();
        }
        order.Total = order.Subtotal - order.DiscountTotal + order.DeliveryFee;
        if (order.Total <= 0) throw new DomainException("Total do pedido inválido.");

        // ---- estoque reservado já no checkout (devolvido se o pagamento falhar/expirar) ----
        foreach (var g in lines.GroupBy(l => l.ProductId))
        {
            var p = found[g.Key];
            var qty = g.Sum(x => x.Quantity);
            p.RemoveStock(qty);
            if (p.TrackStock)
                products.AddStockMovement(new StockMovement { ProductId = p.Id, OrderId = order.Id, Quantity = -qty, Reason = StockReason.Sale });
        }

        // ---- cliente (upsert por CPF dentro da loja) ----
        var customer = await customers.GetByCpfAsync(store.Id, cpf, ct);
        if (customer is null)
        {
            customer = new Customer { StoreId = store.Id, Cpf = cpf };
            customers.Add(customer);
        }
        customer.FullName = order.CustomerName;
        customer.Phone = order.CustomerPhone;
        customer.Email = req.Email?.Trim().ToLowerInvariant();
        order.CustomerId = customer.Id;

        order.History.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PendingPayment });
        orders.Add(order);
        await uow.SaveChangesAsync(ct);

        // Daqui em diante o pedido já existe: não abortar no meio por desconexão do cliente.
        var noCancel = CancellationToken.None;

        GatewayPaymentResult result;
        try
        {
            result = await gateway.CreatePaymentAsync(new GatewayPaymentRequest(
                order.Code, order.Total, req.PaymentMethod,
                customer.Email ?? $"{cpf}@{FallbackEmailDomain}", order.CustomerName, cpf,
                req.Card?.Token, req.Card?.PaymentMethodId, req.Card?.Installments ?? 1, req.Card?.IssuerId,
                IdempotencyKey: order.Id.ToString()), noCancel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha no gateway ao criar pagamento do pedido {Code}", order.Code);
            await lifecycle.CancelAsync(order, null, noCancel);
            await uow.SaveChangesAsync(noCancel);
            throw new PaymentGatewayException("Não foi possível processar o pagamento. Tente novamente.");
        }

        var payment = new Payment
        {
            OrderId = order.Id, Method = req.PaymentMethod, Amount = order.Total, Installments = req.Card?.Installments ?? 1,
            ExternalId = result.ExternalId, PixQrCode = result.PixQrCodeBase64, PixCopyPaste = result.PixCopyPaste,
            PixExpiresAt = result.PixExpiresAt, CardLast4 = result.CardLast4, CardBrand = result.CardBrand,
            RawWebhook = result.RawJson, Status = PaymentStatus.Pending
        };
        payments.Add(payment);
        var becamePaid = await lifecycle.ApplyPaymentStatusAsync(order, payment, result.Status, noCancel);
        await uow.SaveChangesAsync(noCancel);

        if (becamePaid) await lifecycle.NotifyNewOrderAsync(order, noCancel); // cartão aprovado na hora

        return new CheckoutResponse(order.Code, order.Status.ToString(), OrderStatusLabels.Get(order.Status), order.Total,
            new PaymentInfoDto(payment.Method.ToString(), payment.Status.ToString(),
                payment.Status == PaymentStatus.Pending ? payment.PixCopyPaste : null,
                payment.Status == PaymentStatus.Pending ? payment.PixQrCode : null, payment.PixExpiresAt));
    }
}
