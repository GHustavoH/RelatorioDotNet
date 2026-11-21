using Relatorio005.Hub;

namespace Relatorio005.Tests;

public class BuscaMensagensTests
{
    private static readonly Mensagem?[] Massa = GeradorMassaDados.Gerar(5_000, 7);

    [Theory]
    [InlineData("suporte", "pedido", 20)]
    [InlineData("VENDAS", "Reembolso", 5)]
    [InlineData("ti", "senha", 100)]
    [InlineData("rh", "termo-inexistente", 20)]
    [InlineData("juridico", "", 3)]
    public void Otimizada_retorna_o_mesmo_resultado_que_a_baseline(string canal, string termo, int limite)
    {
        var esperado = BuscaMensagens.Baseline(Massa, Massa.Length, canal, termo, limite).Select(m => m.Id).ToArray();
        var obtido = BuscaMensagens.Otimizada(Massa, Massa.Length, canal, termo, limite).Select(m => m.Id).ToArray();

        Assert.Equal(esperado, obtido);
    }

    [Theory]
    [InlineData(EstrategiaBusca.Baseline)]
    [InlineData(EstrategiaBusca.Otimizada)]
    public void Resultado_respeita_limite_filtro_e_ordenacao(EstrategiaBusca estrategia)
    {
        var resultado = BuscaMensagens.Executar(estrategia, Massa, Massa.Length, "suporte", "pedido", 15);

        Assert.True(resultado.Count <= 15);
        Assert.All(resultado, m =>
        {
            Assert.Equal("suporte", m.Canal);
            Assert.Contains("pedido", m.Conteudo);
        });

        for (var i = 1; i < resultado.Count; i++)
        {
            var anterior = resultado[i - 1];
            var atual = resultado[i];
            Assert.True(
                anterior.Prioridade > atual.Prioridade ||
                (anterior.Prioridade == atual.Prioridade && anterior.Sequencia > atual.Sequencia));
        }
    }

    [Fact]
    public void Itens_nulos_sao_ignorados()
    {
        var itens = new Mensagem?[10];
        itens[3] = new Mensagem(Guid.NewGuid(), 1, "ti", "acesso negado", 1, DateTimeOffset.UtcNow);

        Assert.Single(BuscaMensagens.Baseline(itens, itens.Length, "ti", "acesso", 5));
        Assert.Single(BuscaMensagens.Otimizada(itens, itens.Length, "ti", "acesso", 5));
    }

    [Fact]
    public void Gerador_e_deterministico()
    {
        var a = GeradorMassaDados.Gerar(100, 42);
        var b = GeradorMassaDados.Gerar(100, 42);

        Assert.Equal(a.Select(m => m.Id), b.Select(m => m.Id));
        Assert.Equal(a.Select(m => m.Conteudo), b.Select(m => m.Conteudo));
    }
}
