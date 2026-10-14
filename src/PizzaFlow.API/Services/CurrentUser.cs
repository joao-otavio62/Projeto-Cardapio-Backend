using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.API.Services;

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid StoreId => Guid.Parse(Claim("store_id"));
    public Guid UserId => Guid.Parse(Claim("sub"));

    private string Claim(string type) =>
        accessor.HttpContext?.User.FindFirst(type)?.Value ?? throw new AuthenticationFailedException("Não autenticado.");
}
