using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Relatorio005.Hub;

public readonly record struct PedidoMensagem(Guid Id, string Canal, string Conteudo, int Prioridade, long EnfileiradoEm);

public sealed class FilaMensagens
{
    private readonly Channel<PedidoMensagem> _canal;
    private readonly HubMetricas _metricas;

    public FilaMensagens(IOptions<HubOptions> opcoes, HubMetricas metricas)
    {
        _metricas = metricas;
        Capacidade = opcoes.Value.CapacidadeFila;
        _canal = Channel.CreateBounded<PedidoMensagem>(new BoundedChannelOptions(Capacidade)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        _metricas.RegistrarProfundidadeFila(() => _canal.Reader.Count);
    }

    public int Capacidade { get; }

    public int Profundidade => _canal.Reader.Count;

    public ChannelReader<PedidoMensagem> Leitor => _canal.Reader;

    public bool TentarEnfileirar(PedidoMensagem pedido)
    {
        if (_canal.Writer.TryWrite(pedido))
        {
            _metricas.Enfileiradas.Add(1);
            return true;
        }

        _metricas.Rejeitadas.Add(1);
        return false;
    }
}
