using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Tests;

public class ClassificadorPedidoTests
{
    public static TheoryData<decimal, int, Regiao, Prioridade, CategoriaProcessamento> Casos => new()
    {
        // Braço 1 – inválido
        { 0m, 1, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { -1m, 1, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { 100m, 0, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { 100m, -3, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { 100m, 1, (Regiao)0, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { 100m, 1, (Regiao)99, Prioridade.Normal, CategoriaProcessamento.Invalido },
        { 100m, 1, Regiao.Sudeste, (Prioridade)0, CategoriaProcessamento.Invalido },
        { 100m, 1, Regiao.Exterior, (Prioridade)99, CategoriaProcessamento.Invalido },
        { 0m, 0, Regiao.Exterior, Prioridade.Urgente, CategoriaProcessamento.Invalido },

        // Braço 2 – internacional (vence prioridade, valor e volume)
        { 100m, 1, Regiao.Exterior, Prioridade.Baixa, CategoriaProcessamento.Internacional },
        { 100_000m, 100, Regiao.Exterior, Prioridade.Urgente, CategoriaProcessamento.Internacional },
        { 4_999.99m, 50, Regiao.Exterior, Prioridade.Alta, CategoriaProcessamento.Internacional },

        // Braço 3 – expresso (Alta/Urgente com valor < 5000; vence atacado)
        { 100m, 1, Regiao.Sudeste, Prioridade.Urgente, CategoriaProcessamento.Expresso },
        { 100m, 1, Regiao.Sudeste, Prioridade.Alta, CategoriaProcessamento.Expresso },
        { 4_999.99m, 1, Regiao.Sul, Prioridade.Alta, CategoriaProcessamento.Expresso },
        { 100m, 60, Regiao.Sudeste, Prioridade.Urgente, CategoriaProcessamento.Expresso },

        // Braço 4 – atacado (>= 50 itens; vence premium)
        { 100m, 50, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Atacado },
        { 5_000m, 50, Regiao.Sul, Prioridade.Alta, CategoriaProcessamento.Atacado },
        { 9_000m, 120, Regiao.Norte, Prioridade.Baixa, CategoriaProcessamento.Atacado },

        // Braço 5 – premium (valor >= 5000)
        { 5_000m, 1, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Premium },
        { 5_000m, 1, Regiao.Sudeste, Prioridade.Urgente, CategoriaProcessamento.Premium },
        { 5_000m, 49, Regiao.Sul, Prioridade.Baixa, CategoriaProcessamento.Premium },

        // Braço 6 – padrão
        { 4_999.99m, 49, Regiao.Sudeste, Prioridade.Normal, CategoriaProcessamento.Padrao },
        { 0.01m, 1, Regiao.Norte, Prioridade.Baixa, CategoriaProcessamento.Padrao },
        { 100m, 49, Regiao.Nordeste, Prioridade.Normal, CategoriaProcessamento.Padrao },
        { 100m, 1, Regiao.CentroOeste, Prioridade.Baixa, CategoriaProcessamento.Padrao },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Classificar_CadaClasseDeEquivalencia_AlcancaBracoEsperado(
        decimal valor,
        int quantidade,
        Regiao regiao,
        Prioridade prioridade,
        CategoriaProcessamento esperado)
    {
        var categoria = ClassificadorPedido.Classificar(new CriteriosClassificacao(valor, quantidade, regiao, prioridade));

        Assert.Equal(esperado, categoria);
    }

    [Fact]
    public void Classificar_PedidoDeDominio_UsaTotaisDoPedido()
    {
        var item = ItemPedido.Criar("A", 2, 3_000m).Valor!;
        var pedido = Pedido.Criar(PedidoId.Novo(), new[] { item }, Regiao.Sudeste, Prioridade.Normal).Valor!;

        Assert.Equal(CategoriaProcessamento.Premium, ClassificadorPedido.Classificar(pedido));
    }

    [Fact]
    public void Classificar_PedidoNulo_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ClassificadorPedido.Classificar((Pedido)null!));
    }
}
