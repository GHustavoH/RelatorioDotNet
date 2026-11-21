namespace RelatorioFinal.Dominio;

public enum Prioridade
{
    Baixa,
    Normal,
    Alta
}

public enum CategoriaPedido
{
    Invalido,
    Expresso,
    Internacional,
    Corporativo,
    Economico,
    Padrao
}

// identificador forte para nao confundir com outros Guid
public readonly record struct PedidoId(Guid Valor)
{
    public static PedidoId Novo() => new(Guid.NewGuid());

    public static bool TryParse(string? texto, out PedidoId id)
    {
        if (Guid.TryParse(texto, out var guid) && guid != Guid.Empty)
        {
            id = new PedidoId(guid);
            return true;
        }

        id = default;
        return false;
    }

    public override string ToString() => Valor.ToString();
}

public sealed record ItemPedido(string Produto, int Quantidade, decimal Preco)
{
    public decimal Subtotal => Quantidade * Preco;
}

// a lista de itens e somente leitura para nao compartilhar colecao mutavel
public sealed record Pedido(PedidoId Id, string Cliente, string Regiao, Prioridade Prioridade, IReadOnlyList<ItemPedido> Itens)
{
    public decimal Total => Itens.Sum(i => i.Subtotal);
}
