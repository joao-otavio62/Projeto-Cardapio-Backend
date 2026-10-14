using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class AuthService(IAdminUserRepository users, IPasswordHasher hasher, ITokenService tokens, IUnitOfWork uow)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        // sempre executa um Verify (mesmo sem usuário) para não vazar existência do e-mail pelo tempo de resposta
        var valid = hasher.Verify(request.Password, user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !valid || !user.IsActive)
            throw new AuthenticationFailedException("E-mail ou senha inválidos.");

        user.LastLoginAt = DateTime.UtcNow;
        await uow.SaveChangesAsync(ct);

        var (token, expires) = tokens.Generate(user);
        return new LoginResponse(token, expires, new AdminInfoDto(user.Id, user.Name, user.Email, user.StoreId));
    }
}
