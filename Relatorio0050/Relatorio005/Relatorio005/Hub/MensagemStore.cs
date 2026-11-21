using System.Collections.Concurrent;

namespace Relatorio005.Hub;

public sealed class MensagemStore
{
    private readonly Mensagem?[] _itens;
    private readonly ConcurrentDictionary<Guid, Mensagem> _indice = new();
    private int _reservados;
    private long _sequencia;

    public MensagemStore(int capacidade)
    {
        _itens = new Mensagem?[capacidade];
    }

    public int Capacidade => _itens.Length;

    public int Total => Math.Min(Volatile.Read(ref _reservados), _itens.Length);

    public Mensagem?[] Itens => _itens;

    public long ProximaSequencia() => Interlocked.Increment(ref _sequencia);

    public void Carregar(Mensagem[] massa)
    {
        foreach (var mensagem in massa)
        {
            if (!TentarAdicionar(mensagem))
            {
                break;
            }
        }

        var maior = massa.Length == 0 ? 0 : massa.Max(m => m.Sequencia);
        Interlocked.Exchange(ref _sequencia, Math.Max(Interlocked.Read(ref _sequencia), maior));
    }

    public bool TentarAdicionar(Mensagem mensagem)
    {
        var posicao = Interlocked.Increment(ref _reservados) - 1;
        if (posicao >= _itens.Length)
        {
            return false;
        }

        _indice[mensagem.Id] = mensagem;
        Volatile.Write(ref _itens[posicao], mensagem);
        return true;
    }

    public Mensagem? Obter(Guid id) => _indice.TryGetValue(id, out var mensagem) ? mensagem : null;
}
