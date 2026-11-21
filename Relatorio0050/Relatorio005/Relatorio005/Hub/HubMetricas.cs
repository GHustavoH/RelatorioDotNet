using System.Diagnostics.Metrics;

namespace Relatorio005.Hub;

public sealed class HubMetricas : IDisposable
{
    public const string NomeMeter = "Relatorio005.Hub";

    private readonly Meter _meter = new(NomeMeter, "1.0.0");

    public HubMetricas()
    {
        Enfileiradas = _meter.CreateCounter<long>("hub.mensagens.enfileiradas", "{mensagem}", "Mensagens aceitas na fila");
        Processadas = _meter.CreateCounter<long>("hub.mensagens.processadas", "{mensagem}", "Mensagens persistidas pelo worker");
        Rejeitadas = _meter.CreateCounter<long>("hub.mensagens.rejeitadas", "{mensagem}", "Mensagens recusadas por fila cheia");
        ErrosProcessamento = _meter.CreateCounter<long>("hub.mensagens.erros", "{mensagem}", "Falhas no processamento");
        Buscas = _meter.CreateCounter<long>("hub.buscas.total", "{busca}", "Consultas executadas");
        DuracaoProcessamentoMs = _meter.CreateHistogram<double>("hub.processamento.duracao", "ms", "Duracao do processamento por mensagem");
        EsperaNaFilaMs = _meter.CreateHistogram<double>("hub.fila.espera", "ms", "Tempo de espera na fila");
        DuracaoBuscaMs = _meter.CreateHistogram<double>("hub.busca.duracao", "ms", "Duracao do metodo de busca");
    }

    public Counter<long> Enfileiradas { get; }
    public Counter<long> Processadas { get; }
    public Counter<long> Rejeitadas { get; }
    public Counter<long> ErrosProcessamento { get; }
    public Counter<long> Buscas { get; }
    public Histogram<double> DuracaoProcessamentoMs { get; }
    public Histogram<double> EsperaNaFilaMs { get; }
    public Histogram<double> DuracaoBuscaMs { get; }

    public void RegistrarProfundidadeFila(Func<int> leitura) =>
        _meter.CreateObservableGauge("hub.fila.profundidade", leitura, "{mensagem}", "Mensagens aguardando na fila");

    public void Dispose() => _meter.Dispose();
}
