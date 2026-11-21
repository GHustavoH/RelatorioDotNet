using Relatorio003.Dominio;

namespace Relatorio003.Consultas;

public sealed record FiltroPedidos(
    StatusPedido? Status = null,
    string? Cidade = null,
    string? Cliente = null,
    DateTimeOffset? De = null,
    DateTimeOffset? Ate = null);

public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total);

public sealed record PedidoResumoDto(
    int Id,
    string Numero,
    string Cliente,
    StatusPedido Status,
    DateTimeOffset CriadoEm,
    string Cidade,
    int QuantidadeItens,
    decimal Total);

public sealed record ItemRelatorioDto(string Produto, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record PedidoRelatorioDto(
    int Id,
    string Numero,
    string Cliente,
    StatusPedido Status,
    decimal Total,
    List<ItemRelatorioDto> Itens);

public enum EstrategiaRelatorio
{
    
    Projecao,
    IncludeUnico,
    IncludeDividido
}
