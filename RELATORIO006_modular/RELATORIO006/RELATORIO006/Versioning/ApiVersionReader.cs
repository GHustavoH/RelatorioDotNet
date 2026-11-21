using System.Globalization;

namespace RELATORIO006.Versioning;

public enum ApiVersionReadError
{
    None,
    Malformed,
    Conflict,
    Unsupported
}

public readonly record struct ApiVersionReadResult(ApiVersionReadError Error, int Version, string? Raw)
{
    public bool IsValid => Error == ApiVersionReadError.None;
}

/// <summary>Função pura (testável isoladamente) que aplica a política de leitura de versão.</summary>
public static class ApiVersionReader
{
    public static ApiVersionReadResult Read(HttpRequest request, ApiVersioningOptions options)
    {
        var fromHeader = request.Headers[options.HeaderName].ToString().Trim();
        var fromQuery = request.Query[options.QueryParameterName].ToString().Trim();

        var hasHeader = fromHeader.Length > 0;
        var hasQuery = fromQuery.Length > 0;

        if (!hasHeader && !hasQuery)
            return new(ApiVersionReadError.None, options.DefaultVersion, null);

        if (hasHeader && hasQuery && !string.Equals(fromHeader, fromQuery, StringComparison.Ordinal))
            return new(ApiVersionReadError.Conflict, 0, $"{fromHeader} | {fromQuery}");

        var raw = hasHeader ? fromHeader : fromQuery;

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            return new(ApiVersionReadError.Malformed, 0, raw);

        if (!options.SupportedVersions.Contains(version))
            return new(ApiVersionReadError.Unsupported, version, raw);

        return new(ApiVersionReadError.None, version, raw);
    }
}
