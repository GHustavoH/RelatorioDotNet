using System.Collections.Immutable;
using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Comum;

namespace HubPedidos.Domain.Pedidos;

public sealed record Pedido
{
    public const int MaximoItens = 100;

    public PedidoId Id { get; }
    public ImmutableArray<ItemPedido> Itens { get; }
    public Regiao Regiao { get; }
    public Prioridade Prioridade { get; }
    public decimal ValorTotal { get; }
    public int QuantidadeItens { get; }

    private Pedido(PedidoId id, ImmutableArray<ItemPedido> itens, Regiao regiao, Prioridade prioridade)
    {
        Id = id;
        Itens = itens;
        Regiao = regiao;
        Prioridade = prioridade;
        ValorTotal = itens.Sum(i => i.Subtotal);
        QuantidadeItens = itens.Sum(i => i.Quantidade);
    }

    public static Resultado<Pedido> Criar(
        PedidoId? id,
        IEnumerable<ItemPedido>? itens,
        Regiao regiao,
        Prioridade prioridade)
    {
        var erros = new List<string>();

        if (id is null)
        {
            erros.Add("id: obrigatório.");
        }

        var copia = itens is null ? ImmutableArray<ItemPedido>.Empty : itens.ToImmutableArray();

        if (copia.IsEmpty)
        {
            erros.Add("itens: informe ao menos um item.");
        }
        else if (copia.Length > MaximoItens)
        {
            erros.Add($"itens: máximo de {MaximoItens} itens por pedido.");
        }
        else if (copia.Any(i => i is null))
        {
            erros.Add("itens: não pode conter item nulo.");
        }
        else
        {
            var duplicados = copia
                .GroupBy(i => i.Sku, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicados.Length > 0)
            {
                erros.Add($"itens: SKU repetido ({string.Join(", ", duplicados)}).");
            }
        }

        if (!Enum.IsDefined(regiao))
        {
            erros.Add("regiao: valor inválido.");
        }

        if (!Enum.IsDefined(prioridade))
        {
            erros.Add("prioridade: valor inválido.");
        }

        return erros.Count > 0
            ? Resultado<Pedido>.Falha(erros)
            : Resultado<Pedido>.Ok(new Pedido(id!, copia, regiao, prioridade));
    }

    public CriteriosClassificacao ParaClassificacao() =>
        new(ValorTotal, QuantidadeItens, Regiao, Prioridade);

    public bool Equals(Pedido? other) =>
        other is not null
        && Id == other.Id
        && Regiao == other.Regiao
        && Prioridade == other.Prioridade
        && Enumerable.SequenceEqual(Itens, other.Itens);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(Regiao);
        hash.Add(Prioridade);
        foreach (var item in Itens)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
