using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Channels;

namespace RelatorioFinal.Fila;

public sealed class MetricasFila
{
    public static readonly Meter Medidor = new("RelatorioFinal");

    private readonly ConcurrentDictionary<string, long> _contadores = new();
    private readonly Counter<long> _eventos = Medidor.CreateCounter<long>("pedidos_eventos");

    public Histogram<double> Duracao { get; } = Medidor.CreateHistogram<double>("pedido_duracao_ms", "ms");

    public MetricasFila(Channel<ProcessarPedidoJob> canal)
    {
        // profundidade da fila visivel no dotnet-counters
        Medidor.CreateObservableGauge("fila_profundidade", () => canal.Reader.Count);
    }

    // AddOrUpdate e atomico entao e seguro com varios consumidores
    public void Incrementar(string nome)
    {
        _contadores.AddOrUpdate(nome, 1, (_, atual) => atual + 1);
        _eventos.Add(1, new KeyValuePair<string, object?>("tipo", nome));
    }

    public IReadOnlyDictionary<string, long> Snapshot() => new Dictionary<string, long>(_contadores);
}
