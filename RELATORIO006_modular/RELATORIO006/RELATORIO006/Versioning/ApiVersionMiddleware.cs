using System.Globalization;
using Microsoft.Extensions.Options;

namespace RELATORIO006.Versioning;

public static class ApiVersionContext
{
    public const string ItemKey = "Hub.ApiVersion";
}

/// <summary>
/// Resolve a versão ANTES do roteamento (UseRouting vem depois), para que o
/// <see cref="MapToApiVersionAttribute"/> selecione a ação correta.
/// Só atua em /api; o restante (ex.: /openapi) não é versionado.
/// </summary>
public sealed class ApiVersionMiddleware(RequestDelegate next, IOptions<ApiVersioningOptions> options)
{
    private readonly ApiVersioningOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        var supported = string.Join(", ", _options.SupportedVersions);
        context.Response.Headers["api-supported-versions"] = supported;

        var result = ApiVersionReader.Read(context.Request, _options);
        if (!result.IsValid)
        {
            await Reject(context, result, supported);
            return;
        }

        context.Items[ApiVersionContext.ItemKey] = result.Version;
        ApiVersionUsage.Record(result.Version);

        if (_options.Deprecations.TryGetValue(result.Version, out var policy))
        {
            context.Response.OnStarting(() =>
            {
                ApplyDeprecationHeaders(context.Response, policy);
                return Task.CompletedTask;
            });
        }

        await next(context);
    }

    private static void ApplyDeprecationHeaders(HttpResponse response, DeprecationPolicy policy)
    {
        // RFC 9745: Deprecation: @<unix-seconds>   |   RFC 8594: Sunset: <HTTP-date>
        response.Headers["Deprecation"] = "@" + policy.DeprecatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        response.Headers["Sunset"] = policy.SunsetAt.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(policy.InfoLink))
            response.Headers.Append("Link", $"<{policy.InfoLink}>; rel=\"deprecation\"; type=\"text/html\"");
    }

    private Task Reject(HttpContext context, ApiVersionReadResult result, string supported)
    {
        var (title, detail, type) = result.Error switch
        {
            ApiVersionReadError.Unsupported => (
                "Versão de API não suportada",
                $"A versão '{result.Raw}' não é suportada. Versões suportadas: {supported}.",
                "https://hub.example/problems/unsupported-api-version"),
            ApiVersionReadError.Conflict => (
                "Versão de API ambígua",
                $"Header e query informam versões diferentes ('{result.Raw}'). Informe apenas uma ou valores iguais.",
                "https://hub.example/problems/ambiguous-api-version"),
            _ => (
                "Versão de API inválida",
                $"O valor '{result.Raw}' não é uma versão válida. Use um número inteiro. Versões suportadas: {supported}.",
                "https://hub.example/problems/invalid-api-version")
        };

        return Results.Problem(
                title: title,
                detail: detail,
                statusCode: StatusCodes.Status400BadRequest,
                type: type,
                extensions: new Dictionary<string, object?>
                {
                    ["supportedVersions"] = _options.SupportedVersions
                })
            .ExecuteAsync(context);
    }
}
