using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockFlow.Api.Data;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;
using StockFlow.Api.Services;

namespace StockFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly StockFlowDbContext _db;

    public ProductsController(StockFlowDbContext db) => _db = db;

    /// <summary>Lista productos, con búsqueda opcional por SKU o nombre.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAll([FromQuery] string? search, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(p => p.Sku.Contains(term) || p.Name.ToUpper().Contains(term));
        }

        var products = await query.OrderBy(p => p.Sku).ToListAsync(ct);
        return Ok(products.Select(p => p.ToResponse()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken ct)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);
        return Ok(product.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken ct)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await _db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new DomainException($"Ya existe un producto con SKU {sku}.");

        var product = new Product(sku, request.Name, request.Price, request.InitialStock, request.MinStock);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product.ToResponse());
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductResponse>> Update(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.MinStock = request.MinStock;
        product.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);

        return Ok(product.ToResponse());
    }

    /// <summary>Registra una entrada de mercancía (aumenta el stock).</summary>
    [HttpPost("{id:int}/stock-in")]
    public async Task<ActionResult<ProductResponse>> StockIn(int id, StockMovementRequest request, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        product.AddStock(request.Quantity);
        await _db.SaveChangesAsync(ct);
        return Ok(product.ToResponse());
    }
}
