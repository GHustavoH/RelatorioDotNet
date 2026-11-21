using System.Diagnostics.Metrics;

namespace RELATORIO006.Versioning;

/// <summary>
/// Telemetria de uso por versão. O contador "hub.api.requests" (tag api.version) é o insumo
/// do critério de remoção definido em docs/api/lifecycle.md.
/// </summary>
public static class ApiVersionUsage
{
    public const string MeterName = "RELATORIO006.ApiVersioning";
    public const string CounterName = "hub.api.requests";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Requests =
        Meter.CreateCounter<long>(CounterName, description: "Requisições /api por versão do contrato.");

    public static void Record(int version)
        => Requests.Add(1, new KeyValuePair<string, object?>("api.version", version));
}
