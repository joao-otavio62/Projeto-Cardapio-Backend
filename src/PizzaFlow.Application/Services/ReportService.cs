using Microsoft.Extensions.Options;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.Abstractions.Services;
using PizzaFlow.Application.Common;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Exceptions;

namespace PizzaFlow.Application.Services;

public class ReportService(IReportRepository repository, ICurrentUser user, IOptions<ReportOptions> options)
{
    /// <summary>Faturamento considera pedidos pagos e não cancelados. Datas interpretadas no fuso da loja.</summary>
    public async Task<ReportSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var tz = ResolveTimeZone(options.Value.TimeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        var toDate = to ?? today;
        var fromDate = from ?? toDate.AddDays(-29);
        if (fromDate > toDate) throw new DomainException("Período inválido.");
        if (toDate.DayNumber - fromDate.DayNumber > 366) throw new DomainException("Período máximo de 366 dias.");

        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), tz);

        var summary = await repository.GetSummaryAsync(user.StoreId, fromUtc, toUtc, tz, ct);

        // preenche dias sem vendas com zero (gráfico contínuo)
        var byDay = summary.Daily.ToDictionary(d => d.Date);
        var filled = Enumerable.Range(0, toDate.DayNumber - fromDate.DayNumber + 1)
            .Select(i => fromDate.AddDays(i))
            .Select(d => byDay.TryGetValue(d, out var v) ? v : new DailyRevenueDto(d, 0m, 0))
            .ToList();

        return summary with { From = fromDate, To = toDate, Daily = filled };
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT"); // fallback sem tzdata
        }
    }
}
