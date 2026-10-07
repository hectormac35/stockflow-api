namespace StockFlow.Api.Domain;

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Shipped = 2,
    Cancelled = 3
}

/// <summary>
/// Pedido de venta. Las transiciones de estado están encapsuladas aquí para que
/// ninguna capa pueda saltarse las reglas: Draft → Confirmed → Shipped, y
/// Draft/Confirmed → Cancelled.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    public List<OrderLine> Lines { get; set; } = new();

    public decimal Total => Lines.Sum(l => l.LineTotal);

    public void AddLine(Product product, int quantity)
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException("Solo se pueden añadir líneas a un pedido en borrador.");
        if (!product.IsActive)
            throw new DomainException($"El producto {product.Sku} no está activo.");
        if (quantity <= 0)
            throw new DomainException("La cantidad debe ser mayor que cero.");

        var existing = Lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }

        Lines.Add(new OrderLine
        {
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = product.Price // se congela el precio en el momento del pedido
        });
    }

    public void EnsureCanConfirm()
    {
        if (Status != OrderStatus.Draft)
            throw new DomainException($"No se puede confirmar un pedido en estado {Status}.");
        if (Lines.Count == 0)
            throw new DomainException("No se puede confirmar un pedido sin líneas.");
    }

    public void Confirm()
    {
        EnsureCanConfirm();
        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
    }

    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
            throw new DomainException($"Solo se pueden enviar pedidos confirmados (estado actual: {Status}).");

        Status = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;
    }

    /// <summary>Cancela el pedido. Devuelve true si el stock reservado debe devolverse.</summary>
    public bool Cancel()
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Cancelled)
            throw new DomainException($"No se puede cancelar un pedido en estado {Status}.");

        var mustReturnStock = Status == OrderStatus.Confirmed;
        Status = OrderStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        return mustReturnStock;
    }
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
