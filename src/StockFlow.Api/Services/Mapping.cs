using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;

namespace StockFlow.Api.Services;

public static class Mapping
{
    public static ProductResponse ToResponse(this Product p) =>
        new(p.Id, p.Sku, p.Name, p.Price, p.Stock, p.MinStock, p.IsActive, p.IsLowStock);

    public static CustomerResponse ToResponse(this Customer c) =>
        new(c.Id, c.Name, c.Email, c.TaxId);

    public static OrderResponse ToResponse(this Order o) =>
        new(
            o.Id,
            o.Number,
            o.CustomerId,
            o.Customer?.Name ?? string.Empty,
            o.Status.ToString(),
            o.CreatedAt,
            o.ConfirmedAt,
            o.ShippedAt,
            o.CancelledAt,
            o.Total,
            o.Lines
                .Select(l => new OrderLineResponse(
                    l.ProductId,
                    l.Product?.Sku ?? string.Empty,
                    l.Product?.Name ?? string.Empty,
                    l.Quantity,
                    l.UnitPrice,
                    l.LineTotal))
                .ToList());
}
