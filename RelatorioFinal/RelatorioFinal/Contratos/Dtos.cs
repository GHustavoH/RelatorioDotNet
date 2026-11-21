namespace RelatorioFinal.Contratos;

public record ItemRequest(string Produto, int Quantidade, decimal Preco);

public record CriarPedidoRequest(string Cliente, string Regiao, string Prioridade, List<ItemRequest> Itens, string? Cidade = null);

public record PedidoResponse(Guid Id, string Cliente, string Regiao, string Prioridade, string Categoria, string Status, decimal Total, int QuantidadeItens);

// versao 1 do contrato de consulta
public record PedidoV1Response(Guid Id, string Cliente, string Status, decimal Total);

// versao 2 com mudanca incompativel deliberada (cliente virou objeto e status virou situacao)
public record ClienteV2(string Nome, string Regiao);
public record ValorV2(decimal Quantia, string Moeda);
public record ItemV2(string Produto, int Quantidade, decimal PrecoUnitario);
public record PedidoV2Response(Guid Id, ClienteV2 Cliente, string Situacao, ValorV2 Valor, List<ItemV2> Itens);

public record PedidoResumo(Guid Id, string Cliente, string Categoria, string Status, int QuantidadeItens, decimal Total, DateTime CriadoEm);

public record Pagina<T>(IReadOnlyList<T> Itens, int PaginaAtual, int TamanhoPagina, int TotalRegistros);

public record EstatisticaItem(string Nome, int Quantidade);

public record EstatisticasResponse(List<EstatisticaItem> PorStatus, List<EstatisticaItem> PorCategoria);

public record ResumoImportacao(int Total, int Aceitos, int Invalidos, int Rejeitados);

public record PedidoAtualizado(Guid PedidoId, string Status, string Categoria, DateTime AtualizadoEm);

public record LoginRequest(string Usuario, string Senha);

public record LoginResponse(string Token);

public interface INotificadorPedidos
{
    Task NotificarAsync(PedidoAtualizado evento, CancellationToken ct);
}
