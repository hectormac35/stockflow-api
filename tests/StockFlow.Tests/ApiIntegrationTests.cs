using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Api.Data;
using StockFlow.Api.Dtos;
using Xunit;

namespace StockFlow.Tests;

/// <summary>Levanta la API completa en memoria con una base de datos SQLite temporal.</summary>
public class StockFlowFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"stockflow-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<StockFlowDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<StockFlowDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(_dbPath); } catch (IOException) { /* fichero temporal */ }
    }
}

public class ApiIntegrationTests : IClassFixture<StockFlowFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(StockFlowFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Full_order_flow_reserves_stock_and_rejects_overselling()
    {
        var sku = $"IT-{Guid.NewGuid().ToString("N")[..8]}";
        var productResponse = await _client.PostAsJsonAsync("/api/products",
            new { sku, name = "Producto integración", price = 100m, initialStock = 3, minStock = 1 });
        Assert.Equal(HttpStatusCode.Created, productResponse.StatusCode);
        var product = (await productResponse.Content.ReadFromJsonAsync<ProductResponse>())!;

        var customerResponse = await _client.PostAsJsonAsync("/api/customers",
            new { name = "Cliente integración", email = $"{sku}@example.com", taxId = "B12345678" });
        Assert.Equal(HttpStatusCode.Created, customerResponse.StatusCode);
        var customer = (await customerResponse.Content.ReadFromJsonAsync<CustomerResponse>())!;

        // Primer pedido: 2 de 3 unidades → se confirma y quedan 1
        var order1 = await CreateOrder(customer.Id, product.Id, 2);
        var confirm1 = await _client.PostAsync($"/api/orders/{order1.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm1.StatusCode);

        var afterFirst = await _client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}");
        Assert.Equal(1, afterFirst!.Stock);
        Assert.True(afterFirst.IsLowStock);

        // Segundo pedido: 2 unidades con solo 1 disponible → 409 con ProblemDetails
        var order2 = await CreateOrder(customer.Id, product.Id, 2);
        var confirm2 = await _client.PostAsync($"/api/orders/{order2.Id}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, confirm2.StatusCode);

        var problem = await confirm2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Stock insuficiente", problem!.Detail);
    }

    [Fact]
    public async Task Invalid_product_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/products",
            new { sku = "X", name = "", price = -5m, initialStock = 0, minStock = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_returns_404()
    {
        var response = await _client.GetAsync("/api/orders/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<OrderResponse> CreateOrder(int customerId, int productId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/api/orders",
            new { customerId, lines = new[] { new { productId, quantity } } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }
}
