using relatorio007.Models;

namespace relatorio007.Services;

public class PedidoStore
{
    private readonly Dictionary<int, Pedido> _pedidos = new()
    {
        [1] = new Pedido { Id = 1, DonoId = "ana", Status = "Pendente", Total = 100m, Versao = 1, Atualizado = DateTime.UtcNow },
        [2] = new Pedido { Id = 2, DonoId = "bruno", Status = "Pendente", Total = 250m, Versao = 1, Atualizado = DateTime.UtcNow },
        [3] = new Pedido { Id = 3, DonoId = "ana", Status = "Pendente", Total = 80m, Versao = 1, Atualizado = DateTime.UtcNow }
    };

    private readonly object _lock = new();
    private int _leiturasPublicas;

    // quantas vezes a leitura publica chegou ate o store (ou seja, cache miss)
    public int LeiturasPublicas => _leiturasPublicas;

    public Pedido? Obter(int id)
    {
        lock (_lock)
        {
            if (!_pedidos.TryGetValue(id, out var p))
                return null;

            return Copiar(p);
        }
    }

    public Pedido? ObterParaLeituraPublica(int id)
    {
        Interlocked.Increment(ref _leiturasPublicas);
        return Obter(id);
    }

    public Pedido? AlterarStatus(int id, string status)
    {
        lock (_lock)
        {
            if (!_pedidos.TryGetValue(id, out var p))
                return null;

            p.Status = status;
            p.Versao++;
            p.Atualizado = DateTime.UtcNow;
            return Copiar(p);
        }
    }

    private static Pedido Copiar(Pedido p)
    {
        return new Pedido
        {
            Id = p.Id,
            DonoId = p.DonoId,
            Status = p.Status,
            Total = p.Total,
            Versao = p.Versao,
            Atualizado = p.Atualizado
        };
    }
}
