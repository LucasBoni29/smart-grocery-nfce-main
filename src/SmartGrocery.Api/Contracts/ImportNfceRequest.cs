using System.ComponentModel.DataAnnotations;

namespace SmartGrocery.Api.Contracts;

public class ImportNfceRequest
{
    [Required]
    [Url]
    public string NfceUrl { get; set; } = string.Empty;
}