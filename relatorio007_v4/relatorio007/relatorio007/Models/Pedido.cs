namespace relatorio007.Models;

public class Pedido
{
    public int Id { get; set; }
    public string DonoId { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Total { get; set; }
    public int Versao { get; set; }
    public DateTime Atualizado { get; set; }
}

public record PedidoDto(int Id, string Status, decimal Total, int Versao);

public record PedidoPublicoDto(int Id, string Status);

public record AlterarStatusRequest(string Status);

public record PedidoAtualizado(int PedidoId, int Versao, string Status, DateTime Instante);
