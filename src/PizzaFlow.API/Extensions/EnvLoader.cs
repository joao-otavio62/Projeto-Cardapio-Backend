using DotNetEnv;

namespace PizzaFlow.API.Extensions;

/// <summary>
/// Carrega env/.env.{local|dev|prod} conforme APP_ENV (padrão: local) ANTES de o host ser criado.
/// Variáveis já definidas no ambiente (Docker, Render, Azure...) têm prioridade sobre o arquivo.
/// Chaves usam "__" para seções: ConnectionStrings__Default, Jwt__Secret, MercadoPago__AccessToken...
/// </summary>
public static class EnvLoader
{
    public static void Load()
    {
        var appEnv = (Environment.GetEnvironmentVariable("APP_ENV") ?? "local").Trim().ToLowerInvariant();
        var (suffix, aspNetEnv) = appEnv switch
        {
            "prod" or "production" => ("prod", "Production"),
            "dev" or "development" => ("dev", "Development"),
            _ => ("local", "Local")
        };

        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") is null)
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", aspNetEnv);

        var file = FindFile($".env.{suffix}");
        if (file is not null)
            Env.Load(file, new LoadOptions(clobberExistingVars: false));
    }

    private static string? FindFile(string name)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "env", name);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }
}
