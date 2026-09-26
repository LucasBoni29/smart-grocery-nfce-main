namespace SmartGrocery.Api.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Unit { get; set; }
    public string? Barcode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}
