using Microsoft.AspNetCore.Mvc;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // cliente desistiu
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                DomainException => (400, ex.Message),
                NotFoundException => (404, ex.Message),
                ConflictException => (409, ex.Message),
                AuthenticationFailedException => (401, ex.Message),
                PaymentGatewayException => (502, ex.Message),
                BadHttpRequestException => (400, "Requisição inválida."),
                _ => (500, "Erro interno. Tente novamente em instantes.")
            };

            if (status >= 500) logger.LogError(ex, "Erro não tratado em {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status, Title = title, Extensions = { ["traceId"] = context.TraceIdentifier }
            });
        }
    }
}
