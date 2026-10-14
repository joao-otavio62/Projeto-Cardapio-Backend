using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using PizzaFlow.API.Extensions;
using PizzaFlow.API.Filters;
using PizzaFlow.API.Middleware;
using PizzaFlow.API.Services;
using PizzaFlow.Application;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Validators;
using PizzaFlow.Infrastructure;
using PizzaFlow.Infrastructure.Persistence;
using PizzaFlow.Infrastructure.Persistence.Seed;
using PizzaFlow.Infrastructure.Realtime;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

Console.WriteLine(">>> STRING DE CONEXAO: " + config.GetConnectionString("Default"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(config);
builder.Services.AddValidatorsFromAssemblyContaining<CheckoutRequestValidator>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddJwtAuth(config);
builder.Services.AddApiRateLimiting();
builder.Services.AddSwaggerDocs();
builder.Services.AddHealthChecks();

// CORS: Configuração flexível para origens do frontend
var origins = (config["Cors:Origins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origins.Length > 0)
    {
        p.WithOrigins(origins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
    else
    {
        // Fallback local: libera qualquer porta em localhost / 127.0.0.1
        p.SetIsOriginAllowed(origin => new Uri(origin).Host is "localhost" or "127.0.0.1")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    }
}));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();
app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

if (config.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "PizzaFlow API v1"); c.DocumentTitle = "PizzaFlow API"; });
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<AdminNotificationsHub>("/hubs/admin-notifications");
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (config.GetValue<bool>("Database:AutoMigrate"))
    {
        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "Nenhuma migration encontrada. Rode: dotnet ef migrations add Initial -p src/PizzaFlow.Infrastructure -s src/PizzaFlow.API -o Persistence/Migrations");
        await db.Database.MigrateAsync();
    }
    if (config.GetValue<bool>("Seed:Enabled"))
        await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();

public partial class Program { }