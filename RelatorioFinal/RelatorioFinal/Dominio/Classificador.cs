namespace RelatorioFinal.Dominio;

public static class Classificador
{
    // a ordem dos bracos importa porque o primeiro que casar vence
    public static CategoriaPedido Classificar(Pedido pedido) => pedido switch
    {
        { Itens.Count: 0 } => CategoriaPedido.Invalido,
        { Total: <= 0 } => CategoriaPedido.Invalido,
        { Prioridade: Prioridade.Alta } => CategoriaPedido.Expresso,
        { Regiao: "EXTERIOR" } => CategoriaPedido.Internacional,
        { Total: >= 5000 } or { Itens.Count: > 10 } => CategoriaPedido.Corporativo,
        { Total: < 100, Itens.Count: <= 2 } => CategoriaPedido.Economico,
        _ => CategoriaPedido.Padrao
    };
}
