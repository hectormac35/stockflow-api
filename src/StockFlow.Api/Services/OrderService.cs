using Microsoft.EntityFrameworkCore;
using StockFlow.Api.Data;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;

namespace StockFlow.Api.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct);
    Task<OrderResponse> GetAsync(int id, CancellationToken ct);
    Task<PagedResponse<OrderResponse>> ListAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct);
    Task<OrderResponse> ConfirmAsync(int id, CancellationToken ct);
    Task<OrderResponse> ShipAsync(int id, CancellationToken ct);
    Task<OrderResponse> CancelAsync(int id, CancellationToken ct);
}

/// <summary>
/// Orquesta los casos de uso de pedidos. Las reglas viven en las entidades de dominio
/// (Order, Product); este servicio se encarga de cargar datos, aplicar transacciones y guardar.
/// </summary>
public class OrderService : IOrderService
{
    private readonly StockFlowDbContext _db;
    private readonly ILogger<OrderService> _logger;

    public OrderService(StockFlowDbContext db, ILogger<OrderService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new NotFoundException("Cliente", request.CustomerId);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var order = new Order
        {
            CustomerId = customer.Id,
            Customer = customer,
            Number = await NextOrderNumberAsync(ct)
        };

        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                throw new NotFoundException("Producto", line.ProductId);

            order.AddLine(product, line.Quantity);
        }

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Pedido {Number} creado para el cliente {CustomerId} con total {Total}",
            order.Number, customer.Id, order.Total);

        return order.ToResponse();
    }

    public async Task<OrderResponse> GetAsync(int id, CancellationToken ct) =>
        (await LoadOrderAsync(id, ct)).ToResponse();

    public async Task<PagedResponse<OrderResponse>> ListAsync(OrderStatus? status, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (status is { } filter)
            query = query.Where(o => o.Status == filter);

        var total = await query.CountAsync(ct);
        var orders = await query
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<OrderResponse>(orders.Select(o => o.ToResponse()).ToList(), page, pageSize, total);
    }

    public async Task<OrderResponse> ConfirmAsync(int id, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var order = await LoadOrderAsync(id, ct);
        order.EnsureCanConfirm();

        // Se comprueban TODAS las líneas antes de tocar el stock, para devolver
        // un único error con todo lo que falta y no dejar reservas a medias.
        var shortages = order.Lines
            .Where(l => l.Product!.Stock < l.Quantity)
            .Select(l => $"{l.Product!.Sku} (disponible {l.Product.Stock}, solicitado {l.Quantity})")
            .ToList();

        if (shortages.Count > 0)
            throw new DomainException("Stock insuficiente: " + string.Join("; ", shortages));

        foreach (var line in order.Lines)
            line.Product!.RemoveStock(line.Quantity);

        order.Confirm();
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        foreach (var product in order.Lines.Select(l => l.Product!).Where(p => p.IsLowStock))
            _logger.LogWarning("Stock bajo en {Sku}: quedan {Stock} unidades (mínimo {MinStock})",
                product.Sku, product.Stock, product.MinStock);

        return order.ToResponse();
    }

    public async Task<OrderResponse> ShipAsync(int id, CancellationToken ct)
    {
        var order = await LoadOrderAsync(id, ct);
        order.Ship();
        await _db.SaveChangesAsync(ct);
        return order.ToResponse();
    }

    public async Task<OrderResponse> CancelAsync(int id, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var order = await LoadOrderAsync(id, ct);
        var mustReturnStock = order.Cancel();

        if (mustReturnStock)
        {
            foreach (var line in order.Lines)
                line.Product!.AddStock(line.Quantity);
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return order.ToResponse();
    }

    private async Task<Order> LoadOrderAsync(int id, CancellationToken ct) =>
        await _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NotFoundException("Pedido", id);

    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        var prefix = $"PED-{DateTime.UtcNow.Year}-";
        var count = await _db.Orders.CountAsync(o => o.Number.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}
