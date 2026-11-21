namespace HubPedidos.Application.Contratos;

public sealed record CriarPedidoRequest(
    string? Id,
    IReadOnlyList<ItemPedidoRequest>? Itens,
    string? Regiao,
    string? Prioridade);

public sealed record ItemPedidoRequest(
    string? Sku,
    int Quantidade,
    decimal PrecoUnitario);
