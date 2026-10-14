using Microsoft.EntityFrameworkCore;
using PizzaFlow.Application.Abstractions.Repositories;
using PizzaFlow.Application.DTOs;
using PizzaFlow.Domain.Enums;

namespace PizzaFlow.Infrastructure.Persistence.Repositories;

public class ReportRepository(AppDbContext db) : IReportRepository
{
    public async Task<ReportSummaryDto> GetSummaryAsync(Guid storeId, DateTime fromUtc, DateTime toUtc, TimeZoneInfo tz, CancellationToken ct)
    {
        var inPeriod = db.Orders.AsNoTracking().Where(o => o.StoreId == storeId && o.CreatedAt >= fromUtc && o.CreatedAt < toUtc);
        var paid = inPeriod.Where(o => o.Status != OrderStatus.PendingPayment && o.Status != OrderStatus.Cancelled);

        // agrupamento por dia feito em memória para respeitar o fuso da loja (um pedido às 21h BRT já é "amanhã" em UTC)
        var rows = await paid.Select(o => new { o.CreatedAt, o.Total }).ToListAsync(ct);
        var daily = rows
            .GroupBy(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), tz)))
            .Select(g => new DailyRevenueDto(g.Key, g.Sum(x => x.Total), g.Count()))
            .OrderBy(d => d.Date).ToList();

        var revenue = rows.Sum(r => r.Total);
        var count = rows.Count;

        var top = await (from i in db.OrderItems.AsNoTracking()
                         join o in paid on i.OrderId equals o.Id
                         group i by i.ProductName into g
                         orderby g.Sum(x => x.Quantity) descending
                         select new TopProductDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.Total)))
            .Take(10).ToListAsync(ct);

        var byStatusRaw = await inPeriod.GroupBy(o => o.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var byStatus = byStatusRaw.Select(s => new StatusCountDto(s.Status.ToString(), s.Count)).ToList();
        var cancelled = byStatusRaw.FirstOrDefault(s => s.Status == OrderStatus.Cancelled)?.Count ?? 0;

        var lowStock = await db.Products.AsNoTracking()
            .Where(p => p.StoreId == storeId && p.IsActive && p.TrackStock && p.StockQuantity <= p.LowStockAlert)
            .OrderBy(p => p.StockQuantity)
            .Select(p => new LowStockDto(p.Id, p.Name, p.StockQuantity, p.LowStockAlert))
            .ToListAsync(ct);

        return new ReportSummaryDto(default, default, revenue, count, count == 0 ? 0m : Math.Round(revenue / count, 2),
            cancelled, daily, top, byStatus, lowStock);
    }
}
