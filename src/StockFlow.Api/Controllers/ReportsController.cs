using Microsoft.AspNetCore.Mvc;
using StockFlow.Api.Dtos;
using StockFlow.Api.Services;

namespace StockFlow.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Productos activos cuyo stock está en o por debajo del mínimo.</summary>
    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> LowStock(CancellationToken ct)
        => Ok(await _reports.GetLowStockAsync(ct));

    /// <summary>Ventas confirmadas en un rango de fechas (por defecto, últimos 30 días).</summary>
    [HttpGet("sales")]
    public async Task<ActionResult<SalesReportResponse>> Sales(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var end = to ?? DateTime.UtcNow;
        var start = from ?? end.AddDays(-30);
        return Ok(await _reports.GetSalesAsync(start, end, ct));
    }
}
