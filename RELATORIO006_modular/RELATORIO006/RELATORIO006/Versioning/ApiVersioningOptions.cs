namespace RELATORIO006.Versioning;

/// <summary>
/// POLÍTICA DE VERSÃO (ver docs/api/lifecycle.md):
///  - Leitura: header <see cref="HeaderName"/> OU query <see cref="QueryParameterName"/>.
///  - Header e query presentes com valores diferentes => 400.
///  - Ausente => <see cref="DefaultVersion"/> (v1, para não quebrar o cliente anterior).
///  - Mal formada ou não suportada => 400 (application/problem+json) com a lista de versões suportadas.
/// </summary>
public sealed class ApiVersioningOptions
{
    public const string SectionName = "ApiVersioning";

    public int DefaultVersion { get; set; } = 1;
    public string HeaderName { get; set; } = "X-Api-Version";
    public string QueryParameterName { get; set; } = "api-version";
    public int[] SupportedVersions { get; set; } = [1, 2];

    /// <summary>Ciclo de vida por versão (chave = número da versão). Configurável em appsettings.</summary>
    public Dictionary<int, DeprecationPolicy> Deprecations { get; set; } = new();
}

public sealed class DeprecationPolicy
{
    public DateTimeOffset DeprecatedAt { get; set; }
    public DateTimeOffset SunsetAt { get; set; }
    public string? InfoLink { get; set; }
}
