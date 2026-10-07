using Microsoft.AspNetCore.Mvc;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;
using StockFlow.Api.Services;

namespace StockFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;

    public OrdersController(IOrderService orders) => _orders = orders;

    /// <summary>Lista pedidos paginados, filtrando opcionalmente por estado.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> List(
        [FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _orders.ListAsync(status, page, pageSize, ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id, CancellationToken ct)
        => Ok(await _orders.GetAsync(id, ct));

    /// <summary>Crea un pedido en borrador. El precio de cada línea se congela al crearlo.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderResponse>> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>Confirma el pedido y descuenta el stock de forma transaccional.</summary>
    [HttpPost("{id:int}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Confirm(int id, CancellationToken ct)
        => Ok(await _orders.ConfirmAsync(id, ct));

    [HttpPost("{id:int}/ship")]
    public async Task<ActionResult<OrderResponse>> Ship(int id, CancellationToken ct)
        => Ok(await _orders.ShipAsync(id, ct));

    /// <summary>Cancela el pedido. Si estaba confirmado, devuelve el stock.</summary>
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<OrderResponse>> Cancel(int id, CancellationToken ct)
        => Ok(await _orders.CancelAsync(id, ct));
}
