using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Entities;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.Abstractions.Services;

public interface ICurrentUser
{
    Guid StoreId { get; }
    Guid UserId { get; }
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) Generate(AdminUser user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
    /// <summary>Hash válido usado para igualar o tempo de resposta quando o e-mail não existe.</summary>
    string DummyHash { get; }
}

public record GatewayPaymentRequest(string OrderCode, decimal Amount, PaymentMethod Method, string PayerEmail,
    string PayerName, string PayerCpf, string? CardToken, string? CardPaymentMethodId, int Installments,
    string? IssuerId, string IdempotencyKey);

public record GatewayPaymentResult(string ExternalId, PaymentStatus Status, string? PixQrCodeBase64, string? PixCopyPaste,
    DateTime? PixExpiresAt, string? CardLast4, string? CardBrand, string? RawJson);

public interface IPaymentGateway
{
    Task<GatewayPaymentResult> CreatePaymentAsync(GatewayPaymentRequest request, CancellationToken ct = default);
    /// <summary>Fonte da verdade: o webhook só avisa, o status real é sempre reconsultado.</summary>
    Task<GatewayPaymentResult> GetPaymentAsync(string externalId, CancellationToken ct = default);
    bool ValidateWebhookSignature(string dataId, string? signatureHeader, string? requestIdHeader);
}

public record MediaUploadResult(string Url, string PublicId);

public interface IMediaStorage
{
    Task<MediaUploadResult> UploadAsync(Stream content, MediaType type, Guid storeId, CancellationToken ct = default);
    Task DeleteAsync(string publicId, MediaType type, CancellationToken ct = default);
}

public record ChatContext(string StoreSlug, string StoreName);

public interface IChatAssistant
{
    Task<string> ReplyAsync(ChatContext context, IReadOnlyList<ChatMessageDto> history, CancellationToken ct = default);
}

public interface IOrderNotifier
{
    Task NewOrderAsync(Guid storeId, NewOrderNotification notification, CancellationToken ct = default);
    Task OrderStatusChangedAsync(Guid storeId, Guid orderId, string code, OrderStatus status, CancellationToken ct = default);
}
