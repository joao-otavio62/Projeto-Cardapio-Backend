using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Domain.Entities;

public class Customer : BaseEntity
{
    public Guid StoreId { get; set; }
    public string FullName { get; set; } = "";
    /// <summary>Somente dígitos (11).</summary>
    public string Cpf { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class Order : BaseEntity
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Received, OrderStatus.Cancelled],
        [OrderStatus.Received] = [OrderStatus.Preparing, OrderStatus.Cancelled],
        [OrderStatus.Preparing] = [OrderStatus.OutForDelivery, OrderStatus.ReadyForPickup, OrderStatus.Cancelled],
        [OrderStatus.OutForDelivery] = [OrderStatus.Finished],
        [OrderStatus.ReadyForPickup] = [OrderStatus.Finished],
    };

    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string Code { get; set; } = "";
    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;
    public DeliveryType DeliveryType { get; set; }

    // snapshot do cliente no momento da compra
    public string CustomerName { get; set; } = "";
    public string CustomerCpf { get; set; } = "";
    public string? CustomerPhone { get; set; }

    // snapshot do endereço (somente entrega)
    public string? AddrZip { get; set; }
    public string? AddrStreet { get; set; }
    public string? AddrNumber { get; set; }
    public string? AddrComplement { get; set; }
    public string? AddrDistrict { get; set; }
    public string? AddrCity { get; set; }
    public string? AddrState { get; set; }

    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Total { get; set; }
    public DateTime? AdminSeenAt { get; set; }

    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<OrderStatusHistory> History { get; set; } = new();

    public void ChangeStatus(OrderStatus next, Guid? changedBy)
    {
        if (!Allowed.TryGetValue(Status, out var options) || !options.Contains(next))
            throw new DomainException($"Transição de status inválida: {Status} → {next}.");
        if (next == OrderStatus.OutForDelivery && DeliveryType != DeliveryType.Delivery)
            throw new DomainException("Pedido de retirada não pode sair para entrega.");
        if (next == OrderStatus.ReadyForPickup && DeliveryType != DeliveryType.Pickup)
            throw new DomainException("Pedido de entrega não possui status 'pronto para retirada'.");

        Status = next;
        UpdatedAt = DateTime.UtcNow;
        History.Add(new OrderStatusHistory { OrderId = Id, Status = next, ChangedBy = changedBy });
    }
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    /// <summary>Desconto total da linha (unitário x quantidade).</summary>
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
}

public class Payment : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public string Provider { get; set; } = "MERCADOPAGO";
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public int Installments { get; set; } = 1;
    public string? ExternalId { get; set; }
    /// <summary>QR Code PIX em base64 (imagem PNG).</summary>
    public string? PixQrCode { get; set; }
    public string? PixCopyPaste { get; set; }
    public DateTime? PixExpiresAt { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardBrand { get; set; }
    /// <summary>JSON bruto retornado pelo gateway (auditoria).</summary>
    public string? RawWebhook { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    /// <summary>null = sistema/webhook.</summary>
    public Guid? ChangedBy { get; set; }
}
