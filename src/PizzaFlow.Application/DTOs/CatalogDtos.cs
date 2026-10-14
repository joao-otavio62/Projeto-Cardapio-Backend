using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.DTOs;

public record MediaDto(Guid Id, string Type, string Url, int SortOrder);

// Admin
public record ProductDto(Guid Id, Guid CategoryId, string CategoryName, string Name, string? Description,
    decimal Price, decimal FinalPrice, Guid? PromotionId, string? PromotionTitle,
    bool TrackStock, int StockQuantity, int LowStockAlert, bool IsActive, IReadOnlyList<MediaDto> Media);

public record ProductRequest(Guid CategoryId, string Name, string? Description, decimal Price, Guid? PromotionId,
    bool TrackStock, int StockQuantity, int LowStockAlert, bool IsActive);

/// <summary>Quantity é um delta: positivo = entrada, negativo = saída.</summary>
public record StockAdjustRequest(int Quantity);

public record CategoryDto(Guid Id, string Name, int SortOrder, bool IsActive);
public record CategoryRequest(string Name, int SortOrder, bool IsActive);

public record PromotionDto(Guid Id, string Title, DiscountType DiscountType, decimal DiscountValue,
    DateTime StartsAt, DateTime EndsAt, bool IsActive, bool IsCurrent);
public record PromotionRequest(string Title, DiscountType DiscountType, decimal DiscountValue,
    DateTime StartsAt, DateTime EndsAt, bool IsActive);

// Público (não expõe quantidade exata de estoque)
public record PublicProductDto(Guid Id, string Name, string? Description, decimal Price, decimal FinalPrice,
    string? PromotionTitle, bool Available, IReadOnlyList<MediaDto> Media);
public record MenuCategoryDto(Guid Id, string Name, IReadOnlyList<PublicProductDto> Products);
public record MenuResponse(StoreInfoDto Store, IReadOnlyList<MenuCategoryDto> Categories);
