using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Domain.Entities;

public class Category : BaseEntity
{
    public Guid StoreId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = new();
}

public class Promotion : BaseEntity
{
    public Guid StoreId { get; set; }
    public string Title { get; set; } = "";
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsCurrent(DateTime utcNow) => IsActive && utcNow >= StartsAt && utcNow < EndsAt;
}

public class Product : BaseEntity
{
    private string _name = "";

    public Guid StoreId { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public Guid? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    public string Name
    {
        get => _name;
        set { _name = value.Trim(); NameSearch = TextNormalizer.Normalize(value); }
    }
    /// <summary>Nome normalizado (minúsculo, sem acentos) usado na busca fuzzy com pg_trgm.</summary>
    public string NameSearch { get; private set; } = "";

    public string? Description { get; set; }
    public decimal Price { get; set; }
    public bool TrackStock { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockAlert { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public List<ProductMedia> Media { get; set; } = new();

    public decimal DiscountFor(DateTime utcNow)
    {
        if (Promotion is null || !Promotion.IsCurrent(utcNow)) return 0m;
        var discount = Promotion.DiscountType == DiscountType.Percent
            ? Math.Round(Price * Promotion.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero)
            : Promotion.DiscountValue;
        return Math.Min(discount, Price);
    }

    public decimal FinalPrice(DateTime utcNow) => Price - DiscountFor(utcNow);

    public void RemoveStock(int quantity)
    {
        if (!TrackStock) return;
        if (StockQuantity < quantity) throw new DomainException($"Estoque insuficiente para '{Name}'.");
        StockQuantity -= quantity;
    }

    public void AddStock(int quantity)
    {
        if (TrackStock) StockQuantity += quantity;
    }
}

public class ProductMedia : BaseEntity
{
    public Guid ProductId { get; set; }
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = "";
    public string? PublicId { get; set; }
    public int SortOrder { get; set; }
}

public class StockMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid? OrderId { get; set; }
    /// <summary>Negativo = saída.</summary>
    public int Quantity { get; set; }
    public StockReason Reason { get; set; }
}
