using Microsoft.OpenApi.Models;

namespace PizzaFlow.API.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocs(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PizzaFlow API", Version = "v1",
                Description = "Pedidos para pizzarias e restaurantes. Rotas /api/public são abertas ao cliente; /api/admin exigem JWT (botão Authorize)."
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
                In = ParameterLocation.Header, Description = "Cole apenas o token retornado em POST /api/admin/auth/login"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
            });
            var xml = Path.Combine(AppContext.BaseDirectory, "PizzaFlow.API.xml");
            if (File.Exists(xml)) c.IncludeXmlComments(xml);
        });
        return services;
    }
}
