namespace PizzaFlow.Application.DTOs;

public record LoginRequest(string Email, string Password);
public record AdminInfoDto(Guid Id, string Name, string Email, Guid StoreId);
public record LoginResponse(string AccessToken, DateTime ExpiresAt, AdminInfoDto User);

public record StoreInfoDto(Guid Id, string Name, string Slug, string? Phone, decimal DeliveryFee, decimal MinOrder, bool IsOpen);
public record UpdateStoreRequest(string Name, string? Phone, decimal DeliveryFee, decimal MinOrder, bool IsOpen);
