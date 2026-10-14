using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.ValueObjects;

namespace PizzaFlow.Application.Services;

internal static class Mappers
{
    public static MediaDto ToDto(this ProductMedia m) => new(m.Id, m.MediaType.ToString(), m.Url, m.SortOrder);

    public static IReadOnlyList<MediaDto> MediaDtos(Product p) =>
        p.Media.OrderBy(m => m.SortOrder).Select(m => m.ToDto()).ToList();

    public static ProductDto ToAdminDto(this Product p, DateTime now) => new(
        p.Id, p.CategoryId, p.Category?.Name ?? "", p.Name, p.Description, p.Price, p.FinalPrice(now),
        p.PromotionId, p.Promotion?.Title, p.TrackStock, p.StockQuantity, p.LowStockAlert, p.IsActive, MediaDtos(p));

    public static PublicProductDto ToPublicDto(this Product p, DateTime now) => new(
        p.Id, p.Name, p.Description, p.Price, p.FinalPrice(now),
        p.Promotion is { } promo && promo.IsCurrent(now) ? promo.Title : null,
        !p.TrackStock || p.StockQuantity > 0, MediaDtos(p));

    public static StoreInfoDto ToDto(this Store s) => new(s.Id, s.Name, s.Slug, s.Phone, s.DeliveryFee, s.MinOrder, s.IsOpen);

    public static CategoryDto ToDto(this Category c) => new(c.Id, c.Name, c.SortOrder, c.IsActive);

    public static PromotionDto ToDto(this Promotion p, DateTime now) =>
        new(p.Id, p.Title, p.DiscountType, p.DiscountValue, p.StartsAt, p.EndsAt, p.IsActive, p.IsCurrent(now));

    // ---------- Pedidos ----------
    public static PaymentInfoDto? LatestPayment(Order o)
    {
        var p = o.Payments.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        if (p is null) return null;
        var pending = p.Status == PaymentStatus.Pending;
        return new PaymentInfoDto(p.Method.ToString(), p.Status.ToString(),
            pending ? p.PixCopyPaste : null, pending ? p.PixQrCode : null, p.PixExpiresAt);
    }

    private static List<OrderItemDto> Items(Order o) =>
        o.Items.Select(i => new OrderItemDto(i.ProductName, i.Quantity, i.UnitPrice, i.Total, i.Notes)).ToList();

    private static List<StatusHistoryDto> History(Order o) =>
        o.History.OrderBy(h => h.CreatedAt)
            .Select(h => new StatusHistoryDto(h.Status.ToString(), OrderStatusLabels.Get(h.Status), h.CreatedAt)).ToList();

    public static PublicOrderDto ToPublicDto(this Order o) => new(
        o.Code, o.Status.ToString(), OrderStatusLabels.Get(o.Status), o.DeliveryType.ToString(),
        o.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
        o.Total, o.CreatedAt, Items(o), History(o), LatestPayment(o));

    public static AdminOrderListItemDto ToListItem(this Order o) => new(
        o.Id, o.Code, o.Status.ToString(), OrderStatusLabels.Get(o.Status), o.DeliveryType.ToString(),
        o.CustomerName, o.Total, o.AdminSeenAt is not null, o.CreatedAt);

    public static AdminOrderDetailDto ToAdminDetail(this Order o) => new(
        o.Id, o.Code, o.Status.ToString(), OrderStatusLabels.Get(o.Status), o.DeliveryType.ToString(),
        o.CustomerName, Cpf.Normalize(o.CustomerCpf), o.CustomerPhone,
        o.DeliveryType == DeliveryType.Delivery
            ? new AddressDto(o.AddrZip, o.AddrStreet, o.AddrNumber, o.AddrComplement, o.AddrDistrict, o.AddrCity, o.AddrState)
            : null,
        o.Notes, o.Subtotal, o.DiscountTotal, o.DeliveryFee, o.Total, o.AdminSeenAt is not null, o.CreatedAt,
        Items(o), History(o),
        o.Payments.OrderBy(p => p.CreatedAt)
            .Select(p => new AdminPaymentDto(p.Method.ToString(), p.Status.ToString(), p.Amount, p.Installments, p.CardBrand, p.CardLast4, p.PaidAt))
            .ToList());
}
