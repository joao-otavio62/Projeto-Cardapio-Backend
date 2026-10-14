using System.Security.Cryptography;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Application.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public class ReportOptions
{
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

public static class OrderCodeGenerator
{
    // sem caracteres ambíguos (0/O, 1/I)
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return "PZ-" + new string(chars);
    }
}

public static class OrderStatusLabels
{
    public static string Get(OrderStatus s) => s switch
    {
        OrderStatus.PendingPayment => "Aguardando pagamento",
        OrderStatus.Received => "Recebido",
        OrderStatus.Preparing => "Em preparo",
        OrderStatus.OutForDelivery => "Saiu para entrega",
        OrderStatus.ReadyForPickup => "Pronto para retirada",
        OrderStatus.Finished => "Finalizado",
        OrderStatus.Cancelled => "Cancelado",
        _ => s.ToString()
    };
}
