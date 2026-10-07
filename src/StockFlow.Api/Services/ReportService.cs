using Microsoft.EntityFrameworkCore;
using StockFlow.Api.Data;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;

namespace StockFlow.Api.Services;

public interface IReportService
{
    Task<IReadOnlyList<ProductResponse>> GetLowStockAsync(CancellationToken ct);
    Task<SalesReportResponse> GetSalesAsync(DateTime from, DateTime to, CancellationToken ct);
}

public class ReportService : IReportService
{
    private readonly StockFlowDbContext _db;

    public ReportService(StockFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductResponse>> GetLowStockAsync(CancellationToken ct)
    {
        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .OrderBy(p => p.Stock)
            .ToListAsync(ct);

        return products.Select(p => p.ToResponse()).ToList();
    }

    public async Task<SalesReportResponse> GetSalesAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        if (to < from)
            throw new DomainException("La fecha 'to' no puede ser anterior a 'from'.");

        var orders = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .Where(o => (o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Shipped)
                        && o.ConfirmedAt >= from && o.ConfirmedAt <= to)
            .ToListAsync(ct);

        // Los importes se agregan en memoria: SQLite no soporta SUM sobre decimal.
        var topProducts = orders
            .SelectMany(o => o.Lines)
            .GroupBy(l => new { l.ProductId, Sku = l.Product?.Sku ?? "", Name = l.Product?.Name ?? "" })
            .Select(g => new TopProductResponse(g.Key.Sku, g.Key.Name, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(p => p.Revenue)
            .Take(5)
            .ToList();

        return new SalesReportResponse(from, to, orders.Count, orders.Sum(o => o.Total), topProducts);
    }
}
