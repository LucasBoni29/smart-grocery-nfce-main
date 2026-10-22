using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Data;
using SmartGrocery.Api.Models;
using SmartGrocery.Api.Services;

namespace SmartGrocery.Api.Controllers;

[ApiController]
[Route("api/canonical-products")]
public class CanonicalProductsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CanonicalProductMatcher _canonicalProductMatcher;

    public CanonicalProductsController(AppDbContext context, CanonicalProductMatcher canonicalProductMatcher)
    {
        _context = context;
        _canonicalProductMatcher = canonicalProductMatcher;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        return await _context.CanonicalProducts
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Category,
                c.CreatedAt,
                Aliases = c.Products.Select(p => p.Name).ToList()
            })
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var canonical = await _context.CanonicalProducts
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Category,
                c.CreatedAt,
                Products = c.Products.Select(p => new { p.Id, p.Name, p.Barcode, p.Unit })
            })
            .FirstOrDefaultAsync();

        if (canonical is null)
            return NotFound();

        return canonical;
    }

    // Junta dois produtos canonicos que deveriam ser um so (a normalizacao nao pegou o
    // caso). Tudo que apontava pro "fromId" passa a apontar pro "id", e o "fromId" some.
    [HttpPost("{id}/merge/{fromId}")]
    public async Task<IActionResult> Merge(int id, int fromId)
    {
        if (id == fromId)
            return BadRequest(new { message = "Nao da pra juntar um produto canonico com ele mesmo." });

        var target = await _context.CanonicalProducts.FindAsync(id);
        var source = await _context.CanonicalProducts.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == fromId);

        if (target is null || source is null)
            return NotFound();

        foreach (var product in source.Products)
        {
            product.CanonicalProductId = target.Id;
        }

        _context.CanonicalProducts.Remove(source);
        await _context.SaveChangesAsync();

        return Ok(new { message = $"'{source.Name}' juntado em '{target.Name}'.", movedAliases = source.Products.Count });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var canonical = await _context.CanonicalProducts.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
        if (canonical is null)
            return NotFound();

        if (canonical.Products.Count > 0)
        {
            return BadRequest(new { message = "Esse produto canonico ainda tem aliases ligados. Junte (merge) ou desligue antes de apagar." });
        }

        _context.CanonicalProducts.Remove(canonical);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Roda o matcher pra todo Product que ainda nao tem CanonicalProduct (ex: produtos
    // importados antes dessa funcionalidade existir). Nao mexe em quem ja esta linkado.
    [HttpPost("reprocess")]
    public async Task<IActionResult> Reprocess(CancellationToken cancellationToken)
    {
        var unlinked = await _context.Products
            .Where(p => p.CanonicalProductId == null)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var product in unlinked)
        {
            product.CanonicalProduct = await _canonicalProductMatcher.MatchOrCreateAsync(product.Name, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { message = $"{unlinked.Count} produto(s) processado(s).", productIds = unlinked.Select(p => p.Id) });
    }
}
