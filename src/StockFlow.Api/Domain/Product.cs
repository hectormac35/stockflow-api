namespace StockFlow.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; private set; }
    public int MinStock { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsLowStock => Stock <= MinStock;

    public Product() { }

    public Product(string sku, string name, decimal price, int initialStock, int minStock)
    {
        if (price < 0) throw new DomainException("El precio no puede ser negativo.");
        if (initialStock < 0) throw new DomainException("El stock inicial no puede ser negativo.");

        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        Price = price;
        Stock = initialStock;
        MinStock = minStock;
    }

    /// <summary>Descuenta unidades del stock. Falla si no hay suficientes.</summary>
    public void RemoveStock(int quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor que cero.");
        if (quantity > Stock)
            throw new DomainException(
                $"Stock insuficiente para {Sku}: disponible {Stock}, solicitado {quantity}.");

        Stock -= quantity;
    }

    /// <summary>Añade unidades al stock (entrada de mercancía o devolución).</summary>
    public void AddStock(int quantity)
    {
        if (quantity <= 0) throw new DomainException("La cantidad debe ser mayor que cero.");
        Stock += quantity;
    }
}
