using System.Collections.Immutable;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Mapeamento;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class MapeadorPedidoTests
{
    private static CriarPedidoRequest RequestValido() => new(
        Id: null,
        Itens: new List<ItemPedidoRequest> { new("ABC", 2, 10m) },
        Regiao: "sudeste",
        Prioridade: "Normal");

    [Fact]
    public void ParaDominio_RequestValido_Converte()
    {
        var resultado = MapeadorPedido.ParaDominio(RequestValido());

        Assert.True(resultado.TryObter(out var pedido));
        Assert.Equal(Regiao.Sudeste, pedido.Regiao);
        Assert.Equal(Prioridade.Normal, pedido.Prioridade);
        Assert.Equal(20m, pedido.ValorTotal);
    }

    [Fact]
    public void ParaDominio_IdInformado_PreservaIdentificador()
    {
        var id = Guid.NewGuid().ToString();

        var pedido = MapeadorPedido.ParaDominio(RequestValido() with { Id = id }).Valor!;

        Assert.Equal(id, pedido.Id.ToString());
    }

    [Fact]
    public void ParaDominio_RequestNulo_Falha()
    {
        Assert.False(MapeadorPedido.ParaDominio(null).Sucesso);
    }

    [Theory]
    [InlineData("Marte")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("-1")]
    public void ParaDominio_RegiaoInvalida_Falha(string? regiao)
    {
        var resultado = MapeadorPedido.ParaDominio(RequestValido() with { Regiao = regiao });

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith("regiao"));
    }

    [Theory]
    [InlineData("Critica")]
    [InlineData("2")]
    [InlineData(null)]
    public void ParaDominio_PrioridadeInvalida_Falha(string? prioridade)
    {
        var resultado = MapeadorPedido.ParaDominio(RequestValido() with { Prioridade = prioridade });

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith("prioridade"));
    }

    [Fact]
    public void ParaDominio_IdInvalido_Falha()
    {
        var resultado = MapeadorPedido.ParaDominio(RequestValido() with { Id = "nao-e-guid" });

        Assert.Contains(resultado.Erros, e => e.StartsWith("id"));
    }

    [Fact]
    public void ParaDominio_AcumulaErrosDeTodosOsCampos_ComIndiceDoItem()
    {
        var request = new CriarPedidoRequest(
            "x",
            new List<ItemPedidoRequest> { new("OK", 1, 1m), new("", 0, -1m) },
            "Marte",
            "2");

        var resultado = MapeadorPedido.ParaDominio(request);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.StartsWith("id"));
        Assert.Contains(resultado.Erros, e => e.StartsWith("regiao"));
        Assert.Contains(resultado.Erros, e => e.StartsWith("prioridade"));
        Assert.Contains(resultado.Erros, e => e.StartsWith("itens[1].sku"));
        Assert.Contains(resultado.Erros, e => e.StartsWith("itens[1].quantidade"));
        Assert.Contains(resultado.Erros, e => e.StartsWith("itens[1].precoUnitario"));
        Assert.DoesNotContain(resultado.Erros, e => e.StartsWith("itens[0]"));
    }

    [Fact]
    public void ParaDominio_ItensVaziosOuNulos_Falha()
    {
        Assert.False(MapeadorPedido.ParaDominio(RequestValido() with { Itens = null }).Sucesso);
        Assert.False(MapeadorPedido.ParaDominio(RequestValido() with { Itens = new List<ItemPedidoRequest>() }).Sucesso);
    }

    [Fact]
    public void ParaDominio_ListaDoRequestAlteradaDepois_NaoAfetaPedido()
    {
        var itens = new List<ItemPedidoRequest> { new("A", 1, 5m) };
        var pedido = MapeadorPedido.ParaDominio(RequestValido() with { Itens = itens }).Valor!;

        itens.Add(new ItemPedidoRequest("B", 1, 5m));

        Assert.Single(pedido.Itens);
    }

    [Fact]
    public void ParaResponse_ExpoeApenasPrimitivos_ECopiaOsItens()
    {
        var pedido = MapeadorPedido.ParaDominio(RequestValido()).Valor!;
        var descritor = new ProcessadorDescritor(CategoriaProcessamento.Padrao, 1, typeof(ProcessadorPadrao));
        var resultado = new ResultadoProcessamento("ok", 20m);

        var response = MapeadorPedido.ParaResponse(pedido, CategoriaProcessamento.Padrao, descritor, resultado);

        Assert.Equal(pedido.Id.ToString(), response.Id);
        Assert.Equal("Sudeste", response.Regiao);
        Assert.Equal("Padrao", response.Categoria);
        Assert.Equal("ProcessadorPadrao", response.Processador);
        Assert.Equal(1, response.VersaoProcessador);
        Assert.Single(response.Itens);
        Assert.Equal(20m, response.Itens[0].Subtotal);
    }

    [Fact]
    public void Request_With_CriaCopiaIndependente_ComIgualdadePorValorNosEscalares()
    {
        var original = new ItemPedidoRequest("A", 1, 5m);

        var copia = original with { Quantidade = 2 };

        Assert.Equal(1, original.Quantidade);
        Assert.Equal(2, copia.Quantidade);
        Assert.Equal(original, copia with { Quantidade = 1 });
        Assert.NotSame(original, copia);
    }

    [Fact]
    public void Response_With_CriaCopiaEIgualdadeComparaSequenciaDeItens()
    {
        var itens = ImmutableArray.Create(new ItemPedidoResponse("A", 1, 5m, 5m));
        var a = new PedidoResponse { Id = "1", Itens = itens, ValorTotal = 5m };
        var b = new PedidoResponse { Id = "1", Itens = ImmutableArray.Create(new ItemPedidoResponse("A", 1, 5m, 5m)), ValorTotal = 5m };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var diferente = a with { ValorTotal = 6m };
        Assert.NotEqual(a, diferente);
        Assert.Equal(5m, a.ValorTotal);
    }
}
