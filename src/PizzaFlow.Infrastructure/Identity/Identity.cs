using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Entities;

namespace PizzaFlow.Infrastructure.Identity;

public class JwtOptions
{
    public string Issuer { get; set; } = "PizzaFlow";
    public string Audience { get; set; } = "PizzaFlow.Admin";
    public string Secret { get; set; } = "";
    public int ExpiresMinutes { get; set; } = 480;
}

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string Token, DateTime ExpiresAt) Generate(AdminUser user)
    {
        var o = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(o.ExpiresMinutes);

        var claims = new List<System.Security.Claims.Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Name),
            new("role", "Admin"),
            new("store_id", user.StoreId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret));
        var token = new JwtSecurityToken(o.Issuer, o.Audience, claims, now, expires,
            new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public class BCryptPasswordHasher : IPasswordHasher
{
    private static readonly Lazy<string> Dummy = new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 12));

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }

    public string DummyHash => Dummy.Value;
}
