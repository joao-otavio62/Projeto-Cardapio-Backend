using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Common;
using PizzaFlow.Infrastructure.Chatbot;
using PizzaFlow.Infrastructure.Identity;
using PizzaFlow.Infrastructure.Payments;
using PizzaFlow.Infrastructure.Persistence;
using PizzaFlow.Infrastructure.Persistence.Repositories;
using PizzaFlow.Infrastructure.Realtime;
using PizzaFlow.Infrastructure.Storage;

namespace PizzaFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings__Default não configurada (veja env/.env.*.example).");

        services.AddDbContext<AppDbContext>(o => o
            .UseNpgsql(connection, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        // options (valores vêm de variáveis de ambiente: Jwt__Secret, MercadoPago__AccessToken, ...)
        services.Configure<JwtOptions>(config.GetSection("Jwt"));
        services.Configure<MercadoPagoOptions>(config.GetSection("MercadoPago"));
        services.Configure<CloudinaryOptions>(config.GetSection("Cloudinary"));
        services.Configure<AnthropicOptions>(config.GetSection("Anthropic"));
        services.Configure<ReportOptions>(config.GetSection("Reports"));

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IMediaStorage, CloudinaryMediaStorage>();
        services.AddScoped<IOrderNotifier, SignalROrderNotifier>();

        services.AddHttpClient<IPaymentGateway, MercadoPagoGateway>((sp, http) =>
        {
            var o = config.GetSection("MercadoPago").Get<MercadoPagoOptions>() ?? new MercadoPagoOptions();
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IChatAssistant, AnthropicChatAssistant>(http =>
        {
            var o = config.GetSection("Anthropic").Get<AnthropicOptions>() ?? new AnthropicOptions();
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(45);
        });

        services.AddSignalR();
        return services;
    }
}
