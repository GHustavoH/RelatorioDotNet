using Microsoft.AspNetCore.Mvc.ActionConstraints;

namespace RELATORIO006.Versioning;

/// <summary>
/// Seleciona a ação cuja versão coincide com a versão resolvida pelo <see cref="ApiVersionMiddleware"/>.
/// Duas ações com o mesmo verbo e rota coexistem; apenas uma é aceita por requisição.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class MapToApiVersionAttribute(int version) : Attribute, IActionConstraint
{
    public int Version { get; } = version;

    public int Order => 0;

    public bool Accept(ActionConstraintContext context)
        => context.RouteContext.HttpContext.Items.TryGetValue(ApiVersionContext.ItemKey, out var resolved)
           && resolved is int resolvedVersion
           && resolvedVersion == Version;
}
