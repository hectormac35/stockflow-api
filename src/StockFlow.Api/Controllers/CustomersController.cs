using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockFlow.Api.Data;
using StockFlow.Api.Domain;
using StockFlow.Api.Dtos;
using StockFlow.Api.Services;

namespace StockFlow.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly StockFlowDbContext _db;

    public CustomersController(StockFlowDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerResponse>>> GetAll(CancellationToken ct)
    {
        var customers = await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return Ok(customers.Select(c => c.ToResponse()));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomerResponse>> GetById(int id, CancellationToken ct)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Cliente", id);
        return Ok(customer.ToResponse());
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Customers.AnyAsync(c => c.Email == email, ct))
            throw new DomainException($"Ya existe un cliente con el email {email}.");

        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Email = email,
            TaxId = request.TaxId.Trim().ToUpperInvariant()
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer.ToResponse());
    }
}
