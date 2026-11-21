using RelatorioFinal.Contratos;
using RelatorioFinal.Dominio;
using RelatorioFinal.Importacao;
using RelatorioFinal.Servicos;

namespace RelatorioFinal.Tests;

public class ClassificadorTests
{
    private static Pedido Criar(Prioridade prioridade, string regiao, int quantidadeItens, int preco)
    {
        var itens = Enumerable.Range(0, quantidadeItens).Select(_ => new ItemPedido("item", 1, preco)).ToList();
        return new Pedido(PedidoId.Novo(), "ana", regiao, prioridade, itens);
    }

    [Theory]
    [InlineData(Prioridade.Normal, "BRASIL", 0, 10, CategoriaPedido.Invalido)]
    [InlineData(Prioridade.Normal, "BRASIL", 1, 0, CategoriaPedido.Invalido)]
    [InlineData(Prioridade.Alta, "BRASIL", 1, 50, CategoriaPedido.Expresso)]
    [InlineData(Prioridade.Normal, "EXTERIOR", 1, 50, CategoriaPedido.Internacional)]
    [InlineData(Prioridade.Normal, "BRASIL", 1, 5000, CategoriaPedido.Corporativo)]
    [InlineData(Prioridade.Normal, "BRASIL", 11, 1, CategoriaPedido.Corporativo)]
    [InlineData(Prioridade.Normal, "BRASIL", 1, 99, CategoriaPedido.Economico)]
    [InlineData(Prioridade.Normal, "BRASIL", 3, 50, CategoriaPedido.Padrao)]
    public void Classifica_cada_classe_de_equivalencia(Prioridade prioridade, string regiao, int itens, int preco, CategoriaPedido esperado)
    {
        var pedido = Criar(prioridade, regiao, itens, preco);
        Assert.Equal(esperado, Classificador.Classificar(pedido));
    }
}

public class MapeadorTests
{
    [Fact]
    public void Rejeita_quantidade_zero()
    {
        var req = new CriarPedidoRequest("ana", "BRASIL", "Normal", [new ItemRequest("Caneta", 0, 5m)]);
        var resultado = PedidoMapeador.ParaDominio(req);

        Assert.False(resultado.Ok);
        Assert.NotNull(resultado.Erro);
    }

    [Fact]
    public void Aceita_pedido_valido_e_copia_os_itens()
    {
        var itens = new List<ItemRequest> { new("Caneta", 2, 5m) };
        var resultado = PedidoMapeador.ParaDominio(new CriarPedidoRequest("ana", "brasil", "alta", itens));

        Assert.True(resultado.Ok);
        Assert.Equal("BRASIL", resultado.Pedido!.Regiao);

        // mudar a lista original nao pode afetar o dominio
        itens.Add(new ItemRequest("Lapis", 1, 1m));
        Assert.Single(resultado.Pedido.Itens);
    }
}

public class CatalogoTests
{
    [Fact]
    public void Descobre_um_processador_para_cada_categoria_valida()
    {
        var catalogo = new CatalogoProcessadores(typeof(CatalogoProcessadores).Assembly);

        foreach (var categoria in Enum.GetValues<CategoriaPedido>().Where(c => c != CategoriaPedido.Invalido))
        {
            Assert.NotNull(catalogo.Obter(categoria));
        }
    }

    [Fact]
    public void Falha_para_categoria_sem_processador()
    {
        var catalogo = new CatalogoProcessadores(typeof(CatalogoProcessadores).Assembly);
        Assert.Throws<InvalidOperationException>(() => catalogo.Obter(CategoriaPedido.Invalido));
    }
}

public class ImportadorTests
{
    private const string Texto = "ana;BRASIL;Normal;Caneta;3;4.50\r\nbruno;EXTERIOR;Alta;Caderno;10;12.90\n\n";

    [Fact]
    public void Versao_span_e_equivalente_a_baseline()
    {
        var baseline = ImportadorBaseline.Importar(Texto);
        var span = ImportadorSpan.Importar(Texto);

        Assert.Equal(2, baseline.Count);
        Assert.Equal(baseline, span);
    }

    [Fact]
    public void Linha_invalida_gera_erro()
    {
        Assert.Throws<FormatException>(() => ImportadorSpan.Importar("ana;BRASIL;Normal"));
    }

    [Fact]
    public async Task Stream_respeita_o_limite()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Texto));
        await Assert.ThrowsAsync<InvalidDataException>(() => ImportadorStream.ImportarAsync(stream, 10, CancellationToken.None));
    }

    [Fact]
    public async Task Stream_le_as_linhas()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Texto));
        var linhas = await ImportadorStream.ImportarAsync(stream, 1000, CancellationToken.None);
        Assert.Equal(2, linhas.Count);
    }
}

public class ArquiteturaTests
{
    [Fact]
    public void Dominio_e_contratos_nao_dependem_de_infraestrutura()
    {
        var tipos = typeof(Pedido).Assembly.GetTypes()
            .Where(t => t.Namespace is "RelatorioFinal.Dominio" or "RelatorioFinal.Contratos");

        foreach (var tipo in tipos)
        {
            foreach (var propriedade in tipo.GetProperties())
            {
                var usados = new[] { propriedade.PropertyType }.Concat(propriedade.PropertyType.GenericTypeArguments);
                foreach (var usado in usados)
                {
                    var espaco = usado.Namespace ?? "";
                    Assert.False(
                        espaco.StartsWith("Microsoft.EntityFrameworkCore") || espaco.StartsWith("RelatorioFinal.Persistencia"),
                        $"{tipo.Name}.{propriedade.Name} depende de {espaco}");
                }
            }
        }
    }
}
