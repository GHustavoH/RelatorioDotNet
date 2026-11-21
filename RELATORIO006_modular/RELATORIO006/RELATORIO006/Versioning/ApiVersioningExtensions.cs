namespace RELATORIO006.Versioning;

public static class ApiVersioningExtensions
{
    /// <summary>Documentos OpenAPI separados por versão: /openapi/v1.json e /openapi/v2.json.</summary>
    public static readonly string[] DocumentNames = ["v1", "v2"];

    public static IServiceCollection AddHubApiVersioning(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiVersioningOptions>()
            .Bind(configuration.GetSection(ApiVersioningOptions.SectionName));

        foreach (var documentName in DocumentNames)
        {
            var name = documentName;
            services.AddOpenApi(name, options =>
            {
                // Endpoints sem GroupName (ex.: POST /api/orders) aparecem em todas as versões.
                options.ShouldInclude = description => description.GroupName is null || description.GroupName == name;

                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info = new()
                    {
                        Title = $"Hub API - {name}",
                        Version = name,
                        Description = name == "v1"
                            ? "Contrato estável e DEPRECIADO. Ver docs/api/lifecycle.md."
                            : "Contrato atual. Ver docs/api/v1-v2-change-matrix.md."
                    };
                    return Task.CompletedTask;
                });
            });
        }

        return services;
    }

    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder app)
        => app.UseMiddleware<ApiVersionMiddleware>();
}
