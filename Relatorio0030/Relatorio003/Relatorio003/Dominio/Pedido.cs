namespace Relatorio003.Dominio;

public enum StatusPedido
{
    Novo = 0,
    Pago = 1,
    Enviado = 2,
    Entregue = 3,
    Cancelado = 4
}

public class Pedido
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Cliente { get; set; } = "";
    public StatusPedido Status { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public decimal Total { get; set; }

    /// <summary>Tipo complexo mapeado de forma relacional (colunas na tabela Pedidos).</summary>
    public Endereco EnderecoEntrega { get; set; } = new();

    /// <summary>Tipo complexo mapeado como JSON (coluna jsonb).</summary>
    public MetadadosPedido Metadados { get; set; } = new();

    public List<ItemPedido> Itens { get; set; } = [];
}

public class ItemPedido
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }
    public string Produto { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}

public class Endereco
{
    public string Logradouro { get; set; } = "";
    public string Cidade { get; set; } = "";
    public string Uf { get; set; } = "";
    public string Cep { get; set; } = "";
}

public class MetadadosPedido
{
    public string Canal { get; set; } = "";
    public string? Cupom { get; set; }
    public List<string> Tags { get; set; } = [];
}
