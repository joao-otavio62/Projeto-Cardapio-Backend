using PizzaFlow.Domain.Common;

namespace PizzaFlow.Domain.Entities;

public class Store : BaseEntity
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Phone { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal MinOrder { get; set; }
    public bool IsOpen { get; set; } = true;
}

public class AdminUser : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store? Store { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}
