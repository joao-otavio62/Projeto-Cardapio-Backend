using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.DTOs;

// ---- Checkout ----
public record CheckoutItemRequest(Guid ProductId, int Quantity, string? Notes);
public record AddressRequest(string ZipCode, string Street, string Number, string? Complement, string District, string City, string State);
/// <summary>Token gerado no frontend pelo SDK do Mercado Pago. Dados do cartão nunca passam pelo backend.</summary>
public record CardPaymentRequest(string Token, string PaymentMethodId, int Installments, string? IssuerId);

public record CheckoutRequest(string FullName, string Cpf, string? Phone, string? Email,
    DeliveryType DeliveryType, AddressRequest? Address, string? Notes,
    IReadOnlyList<CheckoutItemRequest> Items, PaymentMethod PaymentMethod, CardPaymentRequest? Card);

public record PaymentInfoDto(string Method, string Status, string? PixCopyPaste, string? PixQrCodeBase64, DateTime? PixExpiresAt);
public record CheckoutResponse(string OrderCode, string Status, string StatusLabel, decimal Total, PaymentInfoDto Payment);

// ---- Consulta pública (dados mascarados) ----
public record OrderItemDto(string ProductName, int Quantity, decimal UnitPrice, decimal Total, string? Notes);
public record StatusHistoryDto(string Status, string StatusLabel, DateTime At);
public record PublicOrderDto(string Code, string Status, string StatusLabel, string DeliveryType, string CustomerFirstName,
    decimal Total, DateTime CreatedAt, IReadOnlyList<OrderItemDto> Items, IReadOnlyList<StatusHistoryDto> History, PaymentInfoDto? Payment);
public record OrderLookupRequest(string Cpf);

// ---- Admin ----
public record AdminOrderListItemDto(Guid Id, string Code, string Status, string StatusLabel, string DeliveryType,
    string CustomerName, decimal Total, bool Seen, DateTime CreatedAt);
public record AddressDto(string? ZipCode, string? Street, string? Number, string? Complement, string? District, string? City, string? State);
public record AdminPaymentDto(string Method, string Status, decimal Amount, int Installments, string? CardBrand, string? CardLast4, DateTime? PaidAt);
public record AdminOrderDetailDto(Guid Id, string Code, string Status, string StatusLabel, string DeliveryType,
    string CustomerName, string CustomerCpf, string? CustomerPhone, AddressDto? Address, string? Notes,
    decimal Subtotal, decimal DiscountTotal, decimal DeliveryFee, decimal Total, bool Seen, DateTime CreatedAt,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<StatusHistoryDto> History, IReadOnlyList<AdminPaymentDto> Payments);
public record ChangeStatusRequest(OrderStatus Status);

/// <summary>Payload do evento SignalR "NewOrder" (sino do dashboard).</summary>
public record NewOrderNotification(Guid OrderId, string Code, string CustomerName, decimal Total, string DeliveryType, DateTime CreatedAt);

// ---- Chat ----
public record ChatMessageDto(string Role, string Content);
public record ChatRequest(List<ChatMessageDto> Messages);
public record ChatResponse(string Reply);

// ---- Relatórios ----
public record DailyRevenueDto(DateOnly Date, decimal Revenue, int Orders);
public record TopProductDto(string ProductName, int Quantity, decimal Revenue);
public record StatusCountDto(string Status, int Count);
public record LowStockDto(Guid ProductId, string Name, int StockQuantity, int LowStockAlert);
public record ReportSummaryDto(DateOnly From, DateOnly To, decimal Revenue, int OrdersCount, decimal AverageTicket, int CancelledCount,
    IReadOnlyList<DailyRevenueDto> Daily, IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<StatusCountDto> ByStatus, IReadOnlyList<LowStockDto> LowStock);
