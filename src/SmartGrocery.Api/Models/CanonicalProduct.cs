using System.Text.Json.Serialization;

namespace SmartGrocery.Api.Models;

// Representa o produto "de verdade", independente de como cada mercado escreveu o nome
// na nota (ex: "LEITE ITALAC INT" e "LEITE TP INT ITALAC" apontam pro mesmo CanonicalProduct).
public class CanonicalProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
