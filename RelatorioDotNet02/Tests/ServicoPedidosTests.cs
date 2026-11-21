using HubPedidos.Application;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Pedidos;
using HubPedidos.Application.Processamento;
using Microsoft.Extensions.DependencyInjection;

namespace HubPedidos.Tests;

//Fluxo completo = DTO vai pra domínio - classificação - processador e resposta
public class ServicoPedidosTests
{
    private static ServiceProvider CriarProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPedidoRepositorio, RepositorioEmMemoria>();
        services.AddHubPedidosApplication();
        return services.BuildServiceProvider();
    }

    private static CriarPedidoRequest Request(string regiao, string prioridade, int quantidade, decimal preco, string? id = null) =>
        new(id, new List<ItemPedidoRequest> { new("SKU-1", quantidade, preco) }, regiao, prioridade);

    [Fact]
    public void Registro_ValidaCatalogoEResolucaoDeTodosOsProcessadores()
    {
        using var provider = CriarProvider();

        provider.GetRequiredService<CatalogoProcessadores>().ValidarResolucao(provider);
    }

    [Theory]
    [InlineData("Sudeste", "Normal", 1, 10, "Padrao", "ProcessadorPadrao", 10)]
    [InlineData("Sudeste", "Urgente", 1, 100, "Expresso", "ProcessadorExpressoV2", 104)]
    [InlineData("Sudeste", "Normal", 1, 5000, "Premium", "ProcessadorPremium", 4850)]
    [InlineData("Sudeste", "Normal", 50, 10, "Atacado", "ProcessadorAtacado", 460)]
    [InlineData("Exterior", "Baixa", 1, 100, "Internacional", "ProcessadorInternacional", 112)]
    public async Task Processar_SelecionaProcessadorDaCategoria(
        string regiao, string prioridade, int quantidade, decimal preco,
        string categoriaEsperada, string processadorEsperado, decimal valorFinalEsperado)
    {
        using var provider = CriarProvider();
        using var escopo = provider.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoPedidos>();

        var resultado = await servico.ProcessarAsync(Request(regiao, prioridade, quantidade, preco), CancellationToken.None);

        var criado = Assert.IsType<ResultadoPedido.Criado>(resultado);
        Assert.Equal(categoriaEsperada, criado.Pedido.Categoria);
        Assert.Equal(processadorEsperado, criado.Pedido.Processador);
        Assert.Equal(valorFinalEsperado, criado.Pedido.ValorFinal);
    }

    [Fact]
    public async Task Processar_RequestInvalido_RetornaInvalidoComErros()
    {
        using var provider = CriarProvider();
        using var escopo = provider.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoPedidos>();

        var resultado = await servico.ProcessarAsync(Request("Marte", "Normal", 0, 10), CancellationToken.None);

        var invalido = Assert.IsType<ResultadoPedido.Invalido>(resultado);
        Assert.True(invalido.Erros.Count >= 2);
    }

    [Fact]
    public async Task Processar_MesmoIdDuasVezes_RetornaConflito()
    {
        using var provider = CriarProvider();
        using var escopo = provider.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoPedidos>();
        var id = Guid.NewGuid().ToString();

        var primeiro = await servico.ProcessarAsync(Request("Sul", "Normal", 1, 10, id), CancellationToken.None);
        var segundo = await servico.ProcessarAsync(Request("Sul", "Normal", 1, 10, id), CancellationToken.None);

        Assert.IsType<ResultadoPedido.Criado>(primeiro);
        Assert.IsType<ResultadoPedido.Conflito>(segundo);
    }

    [Fact]
    public void ListarProcessadores_MarcaApenasAVersaoVigente()
    {
        using var provider = CriarProvider();
        using var escopo = provider.CreateScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ServicoPedidos>();

        var lista = servico.ListarProcessadores();

        Assert.Contains(lista, p => p.Nome == "ProcessadorExpressoV1" && !p.Vigente);
        Assert.Contains(lista, p => p.Nome == "ProcessadorExpressoV2" && p.Vigente);
    }
}
