using System.Collections.Concurrent;

namespace Relatorio004.Services;

public class Metricas
{
    private readonly ConcurrentDictionary<string, long> _contadores = new();

    private int _emUsoNoBanco = 0;

    public void Somar(string nome, long valor = 1)
    {
        // AddOrUpdate e atomico, entao nao perde contagem
        _contadores.AddOrUpdate(nome, valor, (chave, atual) => atual + valor);
    }

    public void EntrouNoBanco()
    {
        int agora = Interlocked.Increment(ref _emUsoNoBanco);
        _contadores.AddOrUpdate("maxConcorrenciaBanco", agora, (chave, atual) => Math.Max(atual, agora));
    }

    public void SaiuDoBanco()
    {
        Interlocked.Decrement(ref _emUsoNoBanco);
    }

    public Dictionary<string, long> Todas()
    {
        return new Dictionary<string, long>(_contadores);
    }
}
