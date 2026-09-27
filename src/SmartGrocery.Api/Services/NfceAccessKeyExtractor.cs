using System.Text.RegularExpressions;

namespace SmartGrocery.Api.Services;

public static partial class NfceAccessKeyExtractor
{
    public static bool TryExtract(string nfceUrl, out string accessKey)
    {
        accessKey = string.Empty;

        if (!Uri.TryCreate(nfceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var match = AccessKeyPattern().Match(uri.OriginalString);
        if (!match.Success)
        {
            return false;
        }

        accessKey = match.Value;
        return IsValid(accessKey);
    }

    private static bool IsValid(string accessKey)
    {
        var sum = 0;
        var weight = 4;

        for (var index = 0; index < 43; index++)
        {
            sum += (accessKey[index] - '0') * weight;
            weight = weight == 2 ? 9 : weight - 1;
        }

        var remainder = sum % 11;
        var checkDigit = remainder < 2 ? 0 : 11 - remainder;
        return accessKey[43] - '0' == checkDigit;
    }

    [GeneratedRegex("(?<!\\d)\\d{44}(?!\\d)")]
    private static partial Regex AccessKeyPattern();
}