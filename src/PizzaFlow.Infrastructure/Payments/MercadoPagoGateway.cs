using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Enums;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Infrastructure.Payments;

public class MercadoPagoOptions
{
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";
    public string AccessToken { get; set; } = "";
    /// <summary>Chave secreta do webhook (painel do Mercado Pago > Suas integrações > Webhooks).</summary>
    public string WebhookSecret { get; set; } = "";
    /// <summary>URL pública HTTPS que o Mercado Pago chamará: https://SEU-DOMINIO/api/webhooks/mercadopago</summary>
    public string NotificationUrl { get; set; } = "";
    /// <summary>E-mail usado quando o cliente não informou (o MP exige payer.email).</summary>
    public string FallbackPayerEmail { get; set; } = "";
    public int PixExpirationMinutes { get; set; } = 30;
    /// <summary>Somente para desenvolvimento local. É ignorado em Production.</summary>
    public bool SkipSignatureValidation { get; set; }
}

public class MercadoPagoGateway(HttpClient http, IOptions<MercadoPagoOptions> options, IHostEnvironment env,
    ILogger<MercadoPagoGateway> logger) : IPaymentGateway
{
    private readonly MercadoPagoOptions _o = options.Value;

    public async Task<GatewayPaymentResult> CreatePaymentAsync(GatewayPaymentRequest r, CancellationToken ct)
    {
        var names = r.PayerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var email = r.PayerEmail.EndsWith(".invalid") && !string.IsNullOrWhiteSpace(_o.FallbackPayerEmail) ? _o.FallbackPayerEmail : r.PayerEmail;

        var body = new JsonObject
        {
            ["transaction_amount"] = r.Amount,
            ["description"] = $"Pedido {r.OrderCode}",
            ["external_reference"] = r.OrderCode,
            ["payer"] = new JsonObject
            {
                ["email"] = email,
                ["first_name"] = names.ElementAtOrDefault(0),
                ["last_name"] = names.ElementAtOrDefault(1),
                ["identification"] = new JsonObject { ["type"] = "CPF", ["number"] = r.PayerCpf }
            }
        };
        if (!string.IsNullOrWhiteSpace(_o.NotificationUrl)) body["notification_url"] = _o.NotificationUrl;

        if (r.Method == PaymentMethod.Pix)
        {
            body["payment_method_id"] = "pix";
            body["date_of_expiration"] = DateTimeOffset.UtcNow.AddMinutes(_o.PixExpirationMinutes)
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
        }
        else
        {
            body["token"] = r.CardToken;
            body["payment_method_id"] = r.CardPaymentMethodId;
            body["installments"] = r.Installments;
            if (!string.IsNullOrWhiteSpace(r.IssuerId)) body["issuer_id"] = r.IssuerId;
        }

        using var request = BuildRequest(HttpMethod.Post, "/v1/payments");
        request.Headers.Add("X-Idempotency-Key", r.IdempotencyKey);
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        return await SendAsync(request, ct);
    }

    public async Task<GatewayPaymentResult> GetPaymentAsync(string externalId, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, $"/v1/payments/{Uri.EscapeDataString(externalId)}");
        return await SendAsync(request, ct);
    }

    /// <summary>Valida o header x-signature: HMAC-SHA256 do manifesto "id:{data.id};request-id:{x-request-id};ts:{ts};".</summary>
    public bool ValidateWebhookSignature(string dataId, string? signatureHeader, string? requestIdHeader)
    {
        if (_o.SkipSignatureValidation && !env.IsProduction()) return true;
        if (string.IsNullOrWhiteSpace(_o.WebhookSecret) || string.IsNullOrWhiteSpace(signatureHeader)) return false;

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            else if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }
        if (ts is null || v1 is null) return false;

        var manifest = $"id:{dataId.ToLowerInvariant()};request-id:{requestIdHeader};ts:{ts};";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_o.WebhookSecret), Encoding.UTF8.GetBytes(manifest));
        byte[] received;
        try { received = Convert.FromHexString(v1); } catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(expected, received);
    }

    // ------------------------------------------------------------------
    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.AccessToken);
        return req;
    }

    private async Task<GatewayPaymentResult> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // loga o erro do gateway, mas nunca devolve detalhes ao cliente
            logger.LogError("Mercado Pago retornou {Status}: {Body}", (int)response.StatusCode, json);
            throw new PaymentGatewayException("Falha na comunicação com o gateway de pagamento.");
        }
        return Parse(json);
    }

    private static GatewayPaymentResult Parse(string json)
    {
        var n = JsonNode.Parse(json)!;
        var td = n["point_of_interaction"]?["transaction_data"];
        DateTime? pixExp = DateTimeOffset.TryParse(n["date_of_expiration"]?.GetValue<string>(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d.UtcDateTime : null;

        return new GatewayPaymentResult(
            ExternalId: n["id"]!.ToString(),
            Status: MapStatus(n["status"]?.GetValue<string>(), n["status_detail"]?.GetValue<string>()),
            PixQrCodeBase64: td?["qr_code_base64"]?.GetValue<string>(),
            PixCopyPaste: td?["qr_code"]?.GetValue<string>(),
            PixExpiresAt: td is not null ? pixExp : null,
            CardLast4: n["card"]?["last_four_digits"]?.GetValue<string>(),
            CardBrand: n["payment_method_id"]?.GetValue<string>() is { } m && m != "pix" ? m : null,
            RawJson: Sanitize(n));
    }

    private static PaymentStatus MapStatus(string? status, string? detail) => status switch
    {
        "approved" => PaymentStatus.Approved,
        "rejected" => PaymentStatus.Rejected,
        "cancelled" when detail == "expired" => PaymentStatus.Expired,
        "cancelled" => PaymentStatus.Cancelled,
        "refunded" or "charged_back" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending // pending, in_process, authorized, in_mediation
    };

    /// <summary>Guarda para auditoria só o essencial, sem dados pessoais do pagador.</summary>
    private static string Sanitize(JsonNode n) => new JsonObject
    {
        ["id"] = n["id"]?.DeepClone(),
        ["status"] = n["status"]?.DeepClone(),
        ["status_detail"] = n["status_detail"]?.DeepClone(),
        ["payment_method_id"] = n["payment_method_id"]?.DeepClone(),
        ["payment_type_id"] = n["payment_type_id"]?.DeepClone(),
        ["transaction_amount"] = n["transaction_amount"]?.DeepClone(),
        ["external_reference"] = n["external_reference"]?.DeepClone(),
        ["date_approved"] = n["date_approved"]?.DeepClone(),
    }.ToJsonString();
}
