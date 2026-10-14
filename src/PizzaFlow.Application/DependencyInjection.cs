using Microsoft.Extensions.DependencyInjection;
using PizzaFlow.Application.Services;

namespace PizzaFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<StoreService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<ProductService>();
        services.AddScoped<OrderLifecycleService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderService>();
        services.AddScoped<PaymentWebhookService>();
        services.AddScoped<ReportService>();
        services.AddScoped<ChatService>();
        return services;
    }
}
