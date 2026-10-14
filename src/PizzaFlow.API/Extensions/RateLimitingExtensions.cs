using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PizzaFlow.API.Extensions;

public static class RateLimitPolicies
{
    public const string Login = "login";       // força bruta
    public const string Checkout = "checkout"; // criação de pedidos
    public const string Lookup = "lookup";     // consulta pública por CPF/código (enumeração)
    public const string Chat = "chat";         // custo da API de IA
    public const string Webhook = "webhook";
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
                await ctx.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = 429, Title = "Muitas requisições. Aguarde um instante e tente novamente."
                }, ct);
            };

            // teto global por IP (todas as rotas, inclusive admin)
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                Fixed("global:" + Ip(ctx), 120, 60));

            o.AddPolicy(RateLimitPolicies.Login, ctx => Fixed("login:" + Ip(ctx), 5, 60));
            o.AddPolicy(RateLimitPolicies.Checkout, ctx => Fixed("checkout:" + Ip(ctx), 10, 60));
            o.AddPolicy(RateLimitPolicies.Lookup, ctx => Fixed("lookup:" + Ip(ctx), 10, 60));
            o.AddPolicy(RateLimitPolicies.Chat, ctx => Fixed("chat:" + Ip(ctx), 15, 60));
            o.AddPolicy(RateLimitPolicies.Webhook, ctx => Fixed("webhook:" + Ip(ctx), 600, 60));
        });
        return services;
    }

    private static string Ip(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> Fixed(string key, int permits, int seconds) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0, AutoReplenishment = true
        });
}
