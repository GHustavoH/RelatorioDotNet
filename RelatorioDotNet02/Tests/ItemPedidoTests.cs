using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class ItemPedidoTests
{
    [Fact]
    public void Criar_Valido_CalculaSubtotalEAplicaTrim()
    {
        var resultado = ItemPedido.Criar("  ABC  ", 3, 10.50m);

        Assert.True(resultado.TryObter(out var item));
        Assert.Equal("ABC", item.Sku);
        Assert.Equal(31.50m, item.Subtotal);
    }

    [Theory]
    [InlineData(null, 1, 10.0, "sku")]
    [InlineData("", 1, 10.0, "sku")]
    [InlineData("   ", 1, 10.0, "sku")]
    [InlineData("A", 0, 10.0, "quantidade")]
    [InlineData("A", -1, 10.0, "quantidade")]
    [InlineData("A", 10_001, 10.0, "quantidade")]
    [InlineData("A", 1, 0.0, "precoUnitario")]
    [InlineData("A", 1, -5.0, "precoUnitario")]
    [InlineData("A", 1, 100_000.01, "precoUnitario")]
    public void Criar_Invalido_FalhaComErroDoCampo(string? sku, int quantidade, double preco, string campo)
    {
        var resultado = ItemPedido.Criar(sku, quantidade, (decimal)preco);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith(campo));
    }

    [Fact]
    public void Criar_MaisDeDuasCasasDecimais_Falha()
    {
        var resultado = ItemPedido.Criar("A", 1, 10.123m);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public void Criar_SkuMuitoLongo_Falha()
    {
        var resultado = ItemPedido.Criar(new string('X', ItemPedido.TamanhoMaximoSku + 1), 1, 1m);

        Assert.False(resultado.Sucesso);
    }

    [Fact]
    public void Criar_VariosErros_AcumulaTodos()
    {
        var resultado = ItemPedido.Criar("", 0, -1m);

        Assert.Equal(3, resultado.Erros.Count);
    }

    [Fact]
    public void ItensComMesmosValores_SaoIguais()
    {
        var a = ItemPedido.Criar("A", 2, 5m).Valor;
        var b = ItemPedido.Criar("A", 2, 5m).Valor;

        Assert.Equal(a, b);
        Assert.Equal(a!.GetHashCode(), b!.GetHashCode());
    }

    [Fact]
    public void ItensComValoresDiferentes_NaoSaoIguais()
    {
        var a = ItemPedido.Criar("A", 2, 5m).Valor;
        var b = ItemPedido.Criar("A", 3, 5m).Valor;

        Assert.NotEqual(a, b);
    }
}
