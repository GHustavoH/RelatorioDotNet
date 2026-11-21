namespace RelatorioFinal.Persistencia;

public class PedidoEntidade
{
    public Guid Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Regiao { get; set; } = "";
    public string Prioridade { get; set; } = "";
    public string Categoria { get; set; } = "";
    public string Status { get; set; } = "Recebido";
    public decimal Total { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
    public EnderecoEntrega Endereco { get; set; } = new();
    public MetadadosPedido Metadados { get; set; } = new();
    public List<ItemPedidoEntidade> Itens { get; set; } = new();
}

public class ItemPedidoEntidade
{
    public int Id { get; set; }
    public Guid PedidoId { get; set; }
    public string Produto { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Preco { get; set; }
    public PedidoEntidade? Pedido { get; set; }
}

// tipo complexo mapeado em colunas da propria tabela
public class EnderecoEntrega
{
    public string Pais { get; set; } = "";
    public string Cidade { get; set; } = "";
}

// tipo complexo mapeado como JSON
public class MetadadosPedido
{
    public string Origem { get; set; } = "api";
    public string? Observacao { get; set; }
}
