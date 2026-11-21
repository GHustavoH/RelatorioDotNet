using System.ComponentModel.DataAnnotations;

public class Pedido
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public decimal Valor { get; set; }
    public string Status { get; set; } = "Novo";
    public DateTime CriadoEm { get; set; }
}

public class CriarPedidoRequest
{
    [Required]
    [StringLength(100)]
    public string Cliente { get; set; } = "";

    [Range(0.01, 1000000)]
    public decimal Valor { get; set; }
}

// resposta da versao 1
public record PedidoV1(int Id, string Cliente, decimal Valor);

// resposta da versao 2 (tem status e data)
public record PedidoV2(int Id, string Cliente, decimal Valor, string Status, DateTime CriadoEm);

public record PedidoCriado(int Id, string Cliente, decimal Valor);
