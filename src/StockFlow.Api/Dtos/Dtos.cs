using System.ComponentModel.DataAnnotations;

namespace StockFlow.Api.Dtos;

// ---------- Peticiones ----------

public class CreateProductRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    public string Sku { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int InitialStock { get; set; }

    [Range(0, 1_000_000)]
    public int MinStock { get; set; }
}

public class UpdateProductRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [Range(0, 1_000_000)]
    public int MinStock { get; set; }

    public bool IsActive { get; set; } = true;
}

public class StockMovementRequest
{
    [Range(1, 1_000_000)]
    public int Quantity { get; set; }
}

public class CreateCustomerRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[A-Za-z0-9]{9}$", ErrorMessage = "El NIF/CIF debe tener 9 caracteres alfanuméricos.")]
    public string TaxId { get; set; } = string.Empty;
}

public class CreateOrderRequest
{
    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }

    [Required, MinLength(1, ErrorMessage = "El pedido debe tener al menos una línea.")]
    public List<OrderLineRequest> Lines { get; set; } = new();
}

public class OrderLineRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 10_000)]
    public int Quantity { get; set; }
}

// ---------- Respuestas ----------

public record ProductResponse(int Id, string Sku, string Name, decimal Price, int Stock, int MinStock, bool IsActive, bool IsLowStock);

public record CustomerResponse(int Id, string Name, string Email, string TaxId);

public record OrderLineResponse(int ProductId, string Sku, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal);

public record OrderResponse(
    int Id,
    string Number,
    int CustomerId,
    string CustomerName,
    string Status,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? ShippedAt,
    DateTime? CancelledAt,
    decimal Total,
    IReadOnlyList<OrderLineResponse> Lines);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);

public record SalesReportResponse(DateTime From, DateTime To, int Orders, decimal Revenue, IReadOnlyList<TopProductResponse> TopProducts);

public record TopProductResponse(string Sku, string Name, int UnitsSold, decimal Revenue);
