using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockFlow.Api.Data;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;
using StockFlow.Api.Services;
using Xunit;

namespace StockFlow.Tests;

/// <summary>
/// Tests del servicio de pedidos contra una base de datos SQLite en memoria real,
/// para validar también transacciones y consultas de EF Core.
/// </summary>
public class OrderServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly StockFlowDbContext _db;
    private readonly OrderService _service;
    private readonly int _customerId;
    private readonly int _laptopId;
    private readonly int _mouseId;

    public OrderServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<StockFlowDbContext>().UseSqlite(_connection).Options;
        _db = new StockFlowDbContext(options);
        _db.Database.EnsureCreated();

        var customer = new Customer { Name = "Cliente Test", Email = "test@example.com", TaxId = "B00000000" };
        var laptop = new Product("LAP-T", "Portátil test", 500m, 5, 2);
        var mouse = new Product("RAT-T", "Ratón test", 10m, 100, 10);
        _db.AddRange(customer, laptop, mouse);
        _db.SaveChanges();

        _customerId = customer.Id;
        _laptopId = laptop.Id;
        _mouseId = mouse.Id;
        _service = new OrderService(_db, NullLogger<OrderService>.Instance);
    }

    private Task<OrderResponse> CreateOrder(int laptops, int mice = 0)
    {
        var request = new CreateOrderRequest { CustomerId = _customerId };
        request.Lines.Add(new OrderLineRequest { ProductId = _laptopId, Quantity = laptops });
        if (mice > 0) request.Lines.Add(new OrderLineRequest { ProductId = _mouseId, Quantity = mice });
        return _service.CreateAsync(request, CancellationToken.None);
    }

    private int StockOf(int productId) =>
        _db.Products.AsNoTracking().Single(p => p.Id == productId).Stock;

    [Fact]
    public async Task Create_assigns_sequential_number_and_total()
    {
        var first = await CreateOrder(laptops: 1, mice: 2);
        var second = await CreateOrder(laptops: 1);

        Assert.EndsWith("-0001", first.Number);
        Assert.EndsWith("-0002", second.Number);
        Assert.Equal(520m, first.Total);
        Assert.Equal("Draft", first.Status);
    }

    [Fact]
    public async Task Create_does_not_reserve_stock()
    {
        await CreateOrder(laptops: 3);

        Assert.Equal(5, StockOf(_laptopId));
    }

    [Fact]
    public async Task Confirm_discounts_stock()
    {
        var order = await CreateOrder(laptops: 2, mice: 5);

        var confirmed = await _service.ConfirmAsync(order.Id, CancellationToken.None);

        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal(3, StockOf(_laptopId));
        Assert.Equal(95, StockOf(_mouseId));
    }

    [Fact]
    public async Task Confirm_with_insufficient_stock_fails_without_touching_any_stock()
    {
        var order = await CreateOrder(laptops: 6, mice: 5); // solo hay 5 portátiles

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => _service.ConfirmAsync(order.Id, CancellationToken.None));

        Assert.Contains("LAP-T", ex.Message);
        Assert.Equal(5, StockOf(_laptopId));
        Assert.Equal(100, StockOf(_mouseId));
    }

    [Fact]
    public async Task Cancel_confirmed_order_returns_stock()
    {
        var order = await CreateOrder(laptops: 4);
        await _service.ConfirmAsync(order.Id, CancellationToken.None);

        var cancelled = await _service.CancelAsync(order.Id, CancellationToken.None);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal(5, StockOf(_laptopId));
    }

    [Fact]
    public async Task Get_unknown_order_throws_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(999, CancellationToken.None));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
