using StockFlow.Api.Domain;
using Xunit;

namespace StockFlow.Tests;

public class DomainTests
{
    private static Product NewProduct(int id = 1, int stock = 10, decimal price = 10m)
    {
        var product = new Product($"SKU-{id:D3}", $"Producto {id}", price, stock, 2);
        product.Id = id;
        return product;
    }

    [Fact]
    public void RemoveStock_with_enough_units_decreases_stock()
    {
        var product = NewProduct(stock: 10);

        product.RemoveStock(4);

        Assert.Equal(6, product.Stock);
    }

    [Fact]
    public void RemoveStock_with_insufficient_units_throws_and_keeps_stock()
    {
        var product = NewProduct(stock: 3);

        var ex = Assert.Throws<DomainException>(() => product.RemoveStock(5));

        Assert.Contains("Stock insuficiente", ex.Message);
        Assert.Equal(3, product.Stock);
    }

    [Fact]
    public void AddLine_freezes_unit_price_and_merges_same_product()
    {
        var product = NewProduct(price: 25m);
        var order = new Order();

        order.AddLine(product, 2);
        product.Price = 99m; // un cambio de precio posterior no afecta al pedido
        order.AddLine(product, 1);

        var line = Assert.Single(order.Lines);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(25m, line.UnitPrice);
        Assert.Equal(75m, order.Total);
    }

    [Fact]
    public void AddLine_with_inactive_product_throws()
    {
        var product = NewProduct();
        product.IsActive = false;

        Assert.Throws<DomainException>(() => new Order().AddLine(product, 1));
    }

    [Fact]
    public void Order_follows_valid_lifecycle()
    {
        var order = new Order();
        order.AddLine(NewProduct(), 1);

        order.Confirm();
        order.Ship();

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.NotNull(order.ConfirmedAt);
        Assert.NotNull(order.ShippedAt);
    }

    [Fact]
    public void Cannot_ship_a_draft_order()
    {
        var order = new Order();
        order.AddLine(NewProduct(), 1);

        Assert.Throws<DomainException>(() => order.Ship());
    }

    [Fact]
    public void Cannot_confirm_an_empty_order()
    {
        Assert.Throws<DomainException>(() => new Order().Confirm());
    }

    [Fact]
    public void Cancel_confirmed_order_requests_stock_return()
    {
        var order = new Order();
        order.AddLine(NewProduct(), 1);
        order.Confirm();

        var mustReturnStock = order.Cancel();

        Assert.True(mustReturnStock);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cannot_cancel_a_shipped_order()
    {
        var order = new Order();
        order.AddLine(NewProduct(), 1);
        order.Confirm();
        order.Ship();

        Assert.Throws<DomainException>(() => order.Cancel());
    }
}
