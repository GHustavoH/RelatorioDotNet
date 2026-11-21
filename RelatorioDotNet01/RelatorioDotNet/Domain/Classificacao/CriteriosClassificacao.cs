using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Domain.Classificacao;

public readonly record struct CriteriosClassificacao(
    decimal Valor,
    int QuantidadeItens,
    Regiao Regiao,
    Prioridade Prioridade);
