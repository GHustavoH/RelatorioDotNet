using System.Collections.Immutable;

namespace HubPedidos.Application.Contratos;
public sealed record PedidoResponse
{
    public string Id { get; init; } = string.Empty;
    public ImmutableArray<ItemPedidoResponse> Itens { get; init; } = ImmutableArray<ItemPedidoResponse>.Empty;
    public int QuantidadeItens { get; init; }
    public decimal ValorTotal { get; init; }
    public string Regiao { get; init; } = string.Empty;
    public string Prioridade { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Processador { get; init; } = string.Empty;
    public int VersaoProcessador { get; init; }
    public string DescricaoProcessamento { get; init; } = string.Empty;
    public decimal ValorFinal { get; init; }

    public bool Equals(PedidoResponse? other) =>
        other is not null
        && Id == other.Id
        && QuantidadeItens == other.QuantidadeItens
        && ValorTotal == other.ValorTotal
        && Regiao == other.Regiao
        && Prioridade == other.Prioridade
        && Categoria == other.Categoria
        && Processador == other.Processador
        && VersaoProcessador == other.VersaoProcessador
        && DescricaoProcessamento == other.DescricaoProcessamento
        && ValorFinal == other.ValorFinal
        && Enumerable.SequenceEqual(Itens, other.Itens);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(ValorTotal);
        hash.Add(Categoria);
        hash.Add(Processador);
        hash.Add(VersaoProcessador);
        foreach (var item in Itens)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

public sealed record ItemPedidoResponse(
    string Sku,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record ProcessadorResponse(
    string Categoria,
    int Versao,
    string Nome,
    bool Vigente);
