using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PizzaFlow.API.Extensions;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Application.Services;

namespace PizzaFlow.API.Controllers.Public;

/// <summary>Loja e cardápio (link da loja: /loja/{slug} no frontend).</summary>
[ApiController, AllowAnonymous, Route("api/public/stores/{slug}")]
[Tags("Público - Loja")]
public class PublicStoresController(StoreService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<StoreInfoDto>> Get(string slug, CancellationToken ct) =>
        await service.GetPublicAsync(slug, ct);

    /// <param name="search">Busca fuzzy pelo nome: ignora acentos, maiúsculas e tolera erros de digitação.</param>
    [HttpGet("menu")]
    public async Task<ActionResult<MenuResponse>> Menu(string slug, [FromQuery] string? search, [FromQuery] Guid? categoryId, CancellationToken ct) =>
        await service.GetMenuAsync(slug, search, categoryId, ct);
}

[ApiController, AllowAnonymous, Route("api/public/stores/{slug}/checkout")]
[Tags("Público - Checkout")]
public class PublicCheckoutController(CheckoutService service) : ControllerBase
{
    /// <summary>
    /// Cria o pedido e o pagamento (PIX ou cartão). Preços são recalculados no servidor.
    /// PIX: retorna QR Code/copia-e-cola e o pedido fica "PendingPayment" até o webhook confirmar.
    /// Cartão: o frontend envia o token gerado pelo SDK do Mercado Pago.
    /// </summary>
    [HttpPost, EnableRateLimiting(RateLimitPolicies.Checkout)]
    public async Task<ActionResult<CheckoutResponse>> Checkout(string slug, CheckoutRequest request, CancellationToken ct) =>
        await service.CheckoutAsync(slug, request, ct);
}

[ApiController, AllowAnonymous, Route("api/public/stores/{slug}/orders")]
[Tags("Público - Pedidos")]
public class PublicOrdersController(OrderService service) : ControllerBase
{
    /// <summary>Consulta por código do pedido (não sequencial). Dados pessoais não são retornados.</summary>
    [HttpGet("{code}"), EnableRateLimiting(RateLimitPolicies.Lookup)]
    public async Task<ActionResult<PublicOrderDto>> GetByCode(string slug, string code, CancellationToken ct) =>
        await service.GetPublicByCodeAsync(slug, code, ct);

    /// <summary>Histórico por CPF (POST para o CPF não aparecer em URL/logs). Retorna até 20 pedidos mais recentes.</summary>
    [HttpPost("lookup"), EnableRateLimiting(RateLimitPolicies.Lookup)]
    public async Task<ActionResult<IReadOnlyList<PublicOrderDto>>> LookupByCpf(string slug, OrderLookupRequest request, CancellationToken ct) =>
        Ok(await service.LookupByCpfAsync(slug, request.Cpf, ct));
}

[ApiController, AllowAnonymous, Route("api/public/stores/{slug}/chat")]
[Tags("Público - Chatbot")]
public class PublicChatController(ChatService service) : ControllerBase
{
    /// <summary>Assistente de IA para dúvidas sobre status do pedido (consulta por código ou CPF via ferramenta interna).</summary>
    [HttpPost, EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<ActionResult<ChatResponse>> Chat(string slug, ChatRequest request, CancellationToken ct) =>
        await service.ReplyAsync(slug, request, ct);
}

[ApiController, AllowAnonymous, Route("api/webhooks")]
[Tags("Webhooks")]
public class WebhooksController(PaymentWebhookService service) : ControllerBase
{
    /// <summary>Notificações do Mercado Pago. A assinatura (x-signature) é obrigatória e validada.</summary>
    [HttpPost("mercadopago"), EnableRateLimiting(RateLimitPolicies.Webhook)]
    public async Task<IActionResult> MercadoPago([FromQuery(Name = "data.id")] string? dataIdQuery, CancellationToken ct)
    {
        string? type = Request.Query["type"];
        var dataId = dataIdQuery;

        try
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String) type = t.GetString();
            if (dataId is null && root.TryGetProperty("data", out var d) && d.TryGetProperty("id", out var id))
                dataId = id.ToString();
        }
        catch (JsonException) { /* corpo vazio/ inválido: usa a query string */ }

        if (type is not null && type != "payment") return Ok();        // outros tópicos são ignorados
        if (string.IsNullOrWhiteSpace(dataId)) return BadRequest();

        await service.ProcessAsync(dataId,
            Request.Headers["x-signature"].FirstOrDefault(), Request.Headers["x-request-id"].FirstOrDefault(), ct);
        return Ok();
    }
}
