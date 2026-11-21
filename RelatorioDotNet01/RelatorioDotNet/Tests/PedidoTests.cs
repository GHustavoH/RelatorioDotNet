using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class PedidoTests
{
    private static ItemPedido Item(string sku, int quantidade = 1, decimal preco = 10m) =>
        ItemPedido.Criar(sku, quantidade, preco).Valor!;

    [Fact]
    public void Criar_Valido_CalculaTotais()
    {
        var resultado = Pedido.Criar(
            PedidoId.Novo(),
            new[] { Item("A", 2, 10m), Item("B", 3, 5m) },
            Regiao.Sul,
            Prioridade.Normal);

        Assert.True(resultado.TryObter(out var pedido));
        Assert.Equal(35m, pedido.ValorTotal);
        Assert.Equal(5, pedido.QuantidadeItens);
    }

    [Fact]
    public void Criar_ColecaoOriginalAlterada_NaoAfetaPedido()
    {
        var lista = new List<ItemPedido> { Item("A") };
        var pedido = Pedido.Criar(PedidoId.Novo(), lista, Regiao.Sul, Prioridade.Normal).Valor!;

        lista.Add(Item("B"));
        lista.Clear();

        Assert.Single(pedido.Itens);
        Assert.Equal("A", pedido.Itens[0].Sku);
    }

    [Fact]
    public void Criar_SemItens_Falha()
    {
        var resultado = Pedido.Criar(PedidoId.Novo(), Array.Empty<ItemPedido>(), Regiao.Sul, Prioridade.Normal);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith("itens"));
    }

    [Fact]
    public void Criar_ItensNulo_Falha()
    {
        var resultado = Pedido.Criar(PedidoId.Novo(), null, Regiao.Sul, Prioridade.Normal);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public void Criar_SkuRepetido_Falha()
    {
        var resultado = Pedido.Criar(
            PedidoId.Novo(),
            new[] { Item("A"), Item("a") },
            Regiao.Sul,
            Prioridade.Normal);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("repetido"));
    }

    [Fact]
    public void Criar_AcimaDoLimiteDeItens_Falha()
    {
        var itens = Enumerable.Range(0, Pedido.MaximoItens + 1).Select(i => Item($"SKU{i}"));

        var resultado = Pedido.Criar(PedidoId.Novo(), itens, Regiao.Sul, Prioridade.Normal);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public void Criar_IdNulo_Falha()
    {
        var resultado = Pedido.Criar(null, new[] { Item("A") }, Regiao.Sul, Prioridade.Normal);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith("id"));
    }

    [Fact]
    public void Criar_EnumsForaDoDominio_Falha()
    {
        var resultado = Pedido.Criar(PedidoId.Novo(), new[] { Item("A") }, (Regiao)0, (Prioridade)99);

        Assert.False(resultado.Sucesso);
        Assert.Equal(2, resultado.Erros.Count);
    }

    [Fact]
    public void PedidosComMesmoConteudo_SaoIguais()
    {
        var id = PedidoId.Novo();
        var a = Pedido.Criar(id, new[] { Item("A"), Item("B") }, Regiao.Sul, Prioridade.Alta).Valor!;
        var b = Pedido.Criar(id, new[] { Item("A"), Item("B") }, Regiao.Sul, Prioridade.Alta).Valor!;

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void PedidosComItensEmOrdemDiferente_NaoSaoIguais()
    {
        var id = PedidoId.Novo();
        var a = Pedido.Criar(id, new[] { Item("A"), Item("B") }, Regiao.Sul, Prioridade.Alta).Valor!;
        var b = Pedido.Criar(id, new[] { Item("B"), Item("A") }, Regiao.Sul, Prioridade.Alta).Valor!;

        Assert.NotEqual(a, b);
    }
}
