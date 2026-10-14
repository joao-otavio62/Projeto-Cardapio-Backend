using PizzaFlow.Domain.Common;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;
using PizzaFlow.Domain.ValueObjects;
using Xunit;

namespace PizzaFlow.Domain.Tests;

public class CpfTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("111.111.111-11", false)]
    [InlineData("529.982.247-24", false)]
    [InlineData("123", false)]
    [InlineData(null, false)]
    public void Validates(string? cpf, bool expected) => Assert.Equal(expected, Cpf.IsValid(cpf));

    [Fact] public void Masks() => Assert.Equal("***.982.***-**", Cpf.Mask("52998224725"));
}

public class TextNormalizerTests
{
    [Theory]
    [InlineData("  Calabresa Ç ", "calabresa c")]
    [InlineData("MUÇARELA", "mucarela")]
    [InlineData("Açaí", "acai")]
    public void RemovesAccentsAndCase(string input, string expected) => Assert.Equal(expected, TextNormalizer.Normalize(input));
}

public class OrderTests
{
    private static Order New(DeliveryType t) => new() { DeliveryType = t };

    [Fact]
    public void HappyPath_Delivery()
    {
        var o = New(DeliveryType.Delivery);
        o.ChangeStatus(OrderStatus.Received, null);
        o.ChangeStatus(OrderStatus.Preparing, Guid.NewGuid());
        o.ChangeStatus(OrderStatus.OutForDelivery, Guid.NewGuid());
        o.ChangeStatus(OrderStatus.Finished, Guid.NewGuid());
        Assert.Equal(4, o.History.Count);
    }

    [Fact] public void Cannot_Skip_Steps() =>
        Assert.Throws<DomainException>(() => New(DeliveryType.Delivery).ChangeStatus(OrderStatus.Finished, null));

    [Fact]
    public void Pickup_Cannot_Go_Out_For_Delivery()
    {
        var o = New(DeliveryType.Pickup);
        o.ChangeStatus(OrderStatus.Received, null);
        o.ChangeStatus(OrderStatus.Preparing, null);
        Assert.Throws<DomainException>(() => o.ChangeStatus(OrderStatus.OutForDelivery, null));
        o.ChangeStatus(OrderStatus.ReadyForPickup, null);
    }

    [Fact]
    public void Finished_Cannot_Be_Cancelled()
    {
        var o = New(DeliveryType.Pickup);
        foreach (var s in new[] { OrderStatus.Received, OrderStatus.Preparing, OrderStatus.ReadyForPickup, OrderStatus.Finished })
            o.ChangeStatus(s, null);
        Assert.Throws<DomainException>(() => o.ChangeStatus(OrderStatus.Cancelled, null));
    }
}

public class ProductPricingTests
{
    [Fact]
    public void Percent_Promotion_Applies_Only_In_Period()
    {
        var now = DateTime.UtcNow;
        var p = new Product { Name = "X", Price = 50m, Promotion = new Promotion
            { DiscountType = DiscountType.Percent, DiscountValue = 10, StartsAt = now.AddDays(-1), EndsAt = now.AddDays(1) } };
        Assert.Equal(45m, p.FinalPrice(now));
        Assert.Equal(50m, p.FinalPrice(now.AddDays(2)));
    }

    [Fact]
    public void Stock_Cannot_Go_Negative()
    {
        var p = new Product { Name = "Coca", TrackStock = true, StockQuantity = 2 };
        Assert.Throws<DomainException>(() => p.RemoveStock(3));
        p.RemoveStock(2);
        Assert.Equal(0, p.StockQuantity);
    }
}
