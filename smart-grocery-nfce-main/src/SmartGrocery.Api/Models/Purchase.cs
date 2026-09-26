namespace SmartGrocery.Api.Models;

public class Purchase
{
    public int Id { get; set; }
    public string? StoreName { get; set; }
    public string? NfceUrl { get; set; }
    public DateTime PurchasedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
