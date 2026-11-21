using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Domain.Classificacao;

public static class ClassificadorPedido
{
    public const decimal LimiteValorPremium = 5_000m;
    public const int LimiteQuantidadeAtacado = 50;

    public static CategoriaProcessamento Classificar(Pedido pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        return Classificar(pedido.ParaClassificacao());
    }

    public static CategoriaProcessamento Classificar(CriteriosClassificacao criterios) => criterios switch
    {
        { Valor: <= 0m } or { QuantidadeItens: <= 0 }
            => CategoriaProcessamento.Invalido,

        _ when !Enum.IsDefined(criterios.Regiao) || !Enum.IsDefined(criterios.Prioridade)
            => CategoriaProcessamento.Invalido,

     

        { Regiao: Regiao.Exterior }
            => CategoriaProcessamento.Internacional,

        { Prioridade: Prioridade.Alta or Prioridade.Urgente, Valor: < LimiteValorPremium }
            => CategoriaProcessamento.Expresso,

        { QuantidadeItens: >= LimiteQuantidadeAtacado }
            => CategoriaProcessamento.Atacado,

        { Valor: >= LimiteValorPremium }
            => CategoriaProcessamento.Premium,

        _ => CategoriaProcessamento.Padrao
    };
}
