using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace SmartGrocery.Api.Services;

public sealed partial class NfceDocumentReader(HttpClient httpClient)
{
    public async Task<NfceDocument> ReadAsync(string nfceUrl, CancellationToken cancellationToken)
    {
        var uri = new Uri(nfceUrl);
        if (!uri.Host.EndsWith(".fazenda.sp.gov.br", StringComparison.OrdinalIgnoreCase))
        {
            throw new NfceProcessingException("Apenas URLs publicas da NFC-e de Sao Paulo sao suportadas nesta versao.");
        }

        var html = await httpClient.GetStringAsync(uri, cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var text = Normalize(document.DocumentNode.InnerText);
        if (text.Contains("Problemas na consulta via QR Code", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("QR Code invalido", StringComparison.OrdinalIgnoreCase))
        {
            throw new NfceProcessingException("A SEFAZ recusou a consulta da NFC-e. Confira a URL integral obtida do QR Code.");
        }

        var storeName = GetNodeText(document.DocumentNode, "//*[@id='u20']");
        var purchasedAtValue = FindIssueDate(text);
        var totalValue = GetTotalAmount(document);
        var items = ParseItems(document);

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(storeName))
        {
            missingFields.Add("emitente");
        }

        if (!TryParseDate(purchasedAtValue, out var purchasedAt))
        {
            missingFields.Add("data de emissao");
        }

        if (!TryParseMoney(totalValue, out var totalAmount))
        {
            missingFields.Add("valor a pagar");
        }

        if (items.Count == 0)
        {
            missingFields.Add("itens");
        }

        if (missingFields.Count > 0)
        {
            throw new NfceProcessingException($"A pagina da NFC-e nao apresentou: {string.Join(", ", missingFields)}. Nenhuma compra foi salva.");
        }

        return new NfceDocument(storeName!, purchasedAt, totalAmount, items);
    }

    private static List<NfceDocumentItem> ParseItems(HtmlDocument document)
    {
        var items = new List<NfceDocumentItem>();
        var nodes = document.DocumentNode.SelectNodes("//table[@id='tabResult']/tr")
            ?? new HtmlNodeCollection(null);

        foreach (var node in nodes)
        {
            var name = GetNodeText(node, ".//span[contains(@class, 'txtTit')]");
            var quantityValue = GetNodeText(node, ".//span[contains(@class, 'Rqtd')]");
            var unit = GetNodeText(node, ".//span[contains(@class, 'RUN')]");
            var unitPriceValue = GetNodeText(node, ".//span[contains(@class, 'RvlUnit')]");
            var totalPriceValue = GetNodeText(node, ".//span[contains(@class, 'valor')]");
            var barcodeValue = GetNodeText(node, ".//span[contains(@class, 'RCod')]");

            if (string.IsNullOrWhiteSpace(name) ||
                !TryParseDecimal(RemoveLabel(quantityValue), out var quantity) ||
                !TryParseMoney(RemoveLabel(unitPriceValue), out var unitPrice) ||
                !TryParseMoney(totalPriceValue, out var totalPrice))
            {
                continue;
            }

            items.Add(new NfceDocumentItem(name, RemoveLabel(unit), quantity, unitPrice, totalPrice, ExtractDigits(barcodeValue)));
        }

        return items;
    }

    private static string? FindValue(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var match = Regex.Match(text, $"{Regex.Escape(label)}\\s*:?\\s*(?<value>[^\\r\\n]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups["value"].Value.Trim();
            }
        }

        return null;
    }

    private static string? FindIssueDate(string text)
    {
        var match = Regex.Match(text, "Emissao\\s*:\\s*(?<value>\\d{2}/\\d{2}/\\d{4}\\s+\\d{2}:\\d{2}:\\d{2})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static bool TryParseDate(string? value, out DateTime result)
    {
        return DateTime.TryParse(value, new CultureInfo("pt-BR"), DateTimeStyles.AssumeLocal, out result);
    }

    private static bool TryParseMoney(string? value, out decimal result)
    {
        return decimal.TryParse(value?.Replace("R$", string.Empty), NumberStyles.Number, new CultureInfo("pt-BR"), out result);
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, new CultureInfo("pt-BR"), out result);
    }

    private static string? GetTotalAmount(HtmlDocument document)
    {
        var labels = document.DocumentNode.SelectNodes("//*[@id='totalNota']//label") ?? new HtmlNodeCollection(null);
        foreach (var label in labels)
        {
            if (!Normalize(label.InnerText).StartsWith("Valor a pagar", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return GetNodeText(label.ParentNode, ".//span[contains(@class, 'totalNumb')]");
        }

        return null;
    }

    private static string? GetNodeText(HtmlNode? node, string xpath)
    {
        return node?.SelectSingleNode(xpath) is { } selectedNode ? Normalize(selectedNode.InnerText) : null;
    }

    private static string RemoveLabel(string? value)
    {
        return value is null ? string.Empty : Regex.Replace(value, "^[^:]*:\\s*", string.Empty).Trim();
    }

    private static string? ExtractDigits(string? value)
    {
        var digits = value is null ? string.Empty : string.Concat(value.Where(char.IsDigit));
        return digits.Length > 0 ? digits : null;
    }

    private static string Normalize(string value)
    {
        var decoded = WebUtility.HtmlDecode(value).Normalize(NormalizationForm.FormD);
        var withoutDiacritics = string.Concat(decoded.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));
        return Regex.Replace(withoutDiacritics, "[\\t ]+", " ").Trim();
    }

}

public sealed record NfceDocument(string StoreName, DateTime PurchasedAt, decimal TotalAmount, IReadOnlyList<NfceDocumentItem> Items);

public sealed record NfceDocumentItem(string Name, string Unit, decimal Quantity, decimal UnitPrice, decimal TotalPrice, string? Barcode);

public sealed class NfceProcessingException(string message) : Exception(message);