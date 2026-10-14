using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PizzaFlow.API.Filters;

/// <summary>Valida automaticamente todo argumento de action que possua um IValidator{T} registrado.</summary>
public class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var arg in context.ActionArguments.Values.Where(a => a is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(arg!.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            if (result.IsValid) continue;
            foreach (var g in result.Errors.GroupBy(e => e.PropertyName))
                errors[g.Key] = g.Select(e => e.ErrorMessage).Distinct().ToArray();
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Status = 400, Title = "Dados inválidos."
            });
            return;
        }
        await next();
    }
}
