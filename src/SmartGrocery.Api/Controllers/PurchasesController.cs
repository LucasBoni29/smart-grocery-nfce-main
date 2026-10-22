using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Contracts;
using SmartGrocery.Api.Data;
using SmartGrocery.Api.Models;
using SmartGrocery.Api.Services;

namespace SmartGrocery.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly NfceDocumentReader _nfceDocumentReader;
    private readonly CanonicalProductMatcher _canonicalProductMatcher;

    public PurchasesController(AppDbContext context, NfceDocumentReader nfceDocumentReader, CanonicalProductMatcher canonicalProductMatcher)
    {
        _context = context;
        _nfceDocumentReader = nfceDocumentReader;
        _canonicalProductMatcher = canonicalProductMatcher;
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

    [HttpPost("import-nfce")]
    public async Task<ActionResult<Purchase>> ImportNfce(ImportNfceRequest request, CancellationToken cancellationToken)
    {
        if (!NfceAccessKeyExtractor.TryExtract(request.NfceUrl, out var accessKey))
        {
            ModelState.AddModelError(nameof(request.NfceUrl), "A URL deve conter uma chave de acesso NFC-e valida de 44 digitos.");
            return ValidationProblem(ModelState);
        }

        var existingPurchase = await _context.Purchases
            .Include(purchase => purchase.Items)
            .FirstOrDefaultAsync(purchase => purchase.NfceAccessKey == accessKey, cancellationToken);

        if (existingPurchase is not null && existingPurchase.Items.Count > 0)
        {
            return Conflict(new { message = "Esta NFC-e ja foi importada." });
        }

        NfceDocument document;
        try
        {
            document = await _nfceDocumentReader.ReadAsync(request.NfceUrl, cancellationToken);
        }
        catch (NfceProcessingException exception)
        {
            return UnprocessableEntity(new { message = exception.Message });
        }

        var purchase = existingPurchase ?? new Purchase
        {
            NfceUrl = request.NfceUrl,
            NfceAccessKey = accessKey,
            CreatedAt = DateTime.UtcNow
        };

        purchase.StoreName = document.StoreName;
        purchase.PurchasedAt = document.PurchasedAt.ToUniversalTime();
        purchase.TotalAmount = document.TotalAmount;

        // Chave e so o nome: o "codigo" (Barcode) que a nota traz e interno de cada loja,
        // nao um EAN/GTIN universal, entao nao serve pra casar produto entre lojas diferentes.
        var productsByKey = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in document.Items)
        {
            var productKey = item.Name.Trim();
            if (!productsByKey.TryGetValue(productKey, out var product))
            {
                product = await _context.Products.FirstOrDefaultAsync(
                    candidate => candidate.Name == item.Name,
                    cancellationToken);
            }

            if (product is null)
            {
                var canonicalProduct = await _canonicalProductMatcher.MatchOrCreateAsync(item.Name, cancellationToken);
                product = new Product
                {
                    Name = item.Name,
                    Unit = item.Unit,
                    Barcode = item.Barcode,
                    CanonicalProduct = canonicalProduct,
                    CreatedAt = DateTime.UtcNow
                };
            }

            productsByKey[productKey] = product;

            purchase.Items.Add(new PurchaseItem
            {
                Product = product,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            });
        }

        if (existingPurchase is null)
        {
            _context.Purchases.Add(purchase);
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (existingPurchase is not null)
        {
            return Ok(purchase);
        }

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
