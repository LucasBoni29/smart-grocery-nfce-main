using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Data;
using SmartGrocery.Api.Models;

namespace SmartGrocery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _context;

    public PurchasesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Purchase>>> GetAll()
    {
        return await _context.Purchases
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .OrderByDescending(p => p.PurchasedAt)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Purchase>> GetById(int id)
    {
        var purchase = await _context.Purchases
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (purchase is null)
            return NotFound();

        return purchase;
    }

    [HttpPost]
    public async Task<ActionResult<Purchase>> Create(Purchase purchase)
    {
        purchase.CreatedAt = DateTime.UtcNow;
        _context.Purchases.Add(purchase);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = purchase.Id }, purchase);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var purchase = await _context.Purchases.FindAsync(id);
        if (purchase is null)
            return NotFound();

        _context.Purchases.Remove(purchase);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
