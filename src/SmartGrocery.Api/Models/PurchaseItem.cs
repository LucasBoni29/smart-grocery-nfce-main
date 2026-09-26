namespace SmartGrocery.Api.Models;

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public Purchase Purchase { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public decimal Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    // Stored as provided by the NFC-e document; should equal Quantity * UnitPrice
    public decimal TotalPrice { get; set; }
}
