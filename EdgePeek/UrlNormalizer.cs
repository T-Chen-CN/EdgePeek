namespace EdgePeek;

public static class UrlNormalizer
{
    public const string DefaultUrl = "https://www.bing.com";

    public static string NormalizeAddress(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultUrl;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        if (value.Contains('.') && !value.Contains(' '))
        {
            return $"https://{value}";
        }

        return $"https://www.bing.com/search?q={Uri.EscapeDataString(value)}";
    }

    public static string NormalizeHomeUrl(string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultUrl;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return $"https://{value}";
    }
}
