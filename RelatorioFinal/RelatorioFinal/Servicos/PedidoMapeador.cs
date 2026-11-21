using RelatorioFinal.Contratos;
using RelatorioFinal.Dominio;
using RelatorioFinal.Persistencia;

namespace RelatorioFinal.Servicos;

public record ResultadoMapeamento(Pedido? Pedido, string? Erro)
{
    public bool Ok => Pedido is not null;
}

public static class PedidoMapeador
{
    // fronteira onde os dados invalidos sao barrados antes de chegar no dominio
    public static ResultadoMapeamento ParaDominio(CriarPedidoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Cliente))
        {
            return new(null, "Cliente obrigatorio");
        }

        if (req.Cliente.Length > 100)
        {
            return new(null, "Cliente deve ter no maximo 100 caracteres");
        }

        var textoPrioridade = string.IsNullOrWhiteSpace(req.Prioridade) ? "Normal" : req.Prioridade;
        if (!Enum.TryParse<Prioridade>(textoPrioridade, true, out var prioridade) || !Enum.IsDefined(prioridade))
        {
            return new(null, "Prioridade invalida");
        }

        if (req.Itens is null || req.Itens.Count == 0)
        {
            return new(null, "Pedido sem itens");
        }

        var itens = new List<ItemPedido>();
        foreach (var item in req.Itens)
        {
            if (string.IsNullOrWhiteSpace(item.Produto))
            {
                return new(null, "Produto obrigatorio");
            }

            if (item.Quantidade <= 0)
            {
                return new(null, "Quantidade deve ser maior que zero");
            }

            if (item.Preco <= 0)
            {
                return new(null, "Preco deve ser maior que zero");
            }

            itens.Add(new ItemPedido(item.Produto.Trim(), item.Quantidade, item.Preco));
        }

        var regiao = string.IsNullOrWhiteSpace(req.Regiao) ? "BRASIL" : req.Regiao.Trim();

        // copia a lista para nao dividir a colecao do DTO
        var pedido = new Pedido(PedidoId.Novo(), req.Cliente.Trim(), regiao.ToUpperInvariant(), prioridade, itens.AsReadOnly());
        return new(pedido, null);
    }

    public static PedidoEntidade ParaEntidade(Pedido pedido, CategoriaPedido categoria, string? cidade)
    {
        var agora = DateTime.UtcNow;
        return new PedidoEntidade
        {
            Id = pedido.Id.Valor,
            Cliente = pedido.Cliente,
            Regiao = pedido.Regiao,
            Prioridade = pedido.Prioridade.ToString(),
            Categoria = categoria.ToString(),
            Status = "Recebido",
            Total = pedido.Total,
            CriadoEm = agora,
            AtualizadoEm = agora,
            Endereco = new EnderecoEntrega { Pais = pedido.Regiao, Cidade = cidade ?? "" },
            Metadados = new MetadadosPedido { Origem = "api" },
            Itens = pedido.Itens
                .Select(i => new ItemPedidoEntidade { Produto = i.Produto, Quantidade = i.Quantidade, Preco = i.Preco })
                .ToList()
        };
    }

    public static Pedido EntidadeParaDominio(PedidoEntidade e) =>
        new(
            new PedidoId(e.Id),
            e.Cliente,
            e.Regiao,
            Enum.Parse<Prioridade>(e.Prioridade),
            e.Itens.Select(i => new ItemPedido(i.Produto, i.Quantidade, i.Preco)).ToList().AsReadOnly());

    public static PedidoResponse ParaResponse(PedidoEntidade e) =>
        new(e.Id, e.Cliente, e.Regiao, e.Prioridade, e.Categoria, e.Status, e.Total, e.Itens.Count);

    public static PedidoV1Response ParaV1(PedidoEntidade e) =>
        new(e.Id, e.Cliente, e.Status, e.Total);

    public static PedidoV2Response ParaV2(PedidoEntidade e) =>
        new(
            e.Id,
            new ClienteV2(e.Cliente, e.Regiao),
            e.Status,
            new ValorV2(e.Total, "BRL"),
            e.Itens.Select(i => new ItemV2(i.Produto, i.Quantidade, i.Preco)).ToList());
}
