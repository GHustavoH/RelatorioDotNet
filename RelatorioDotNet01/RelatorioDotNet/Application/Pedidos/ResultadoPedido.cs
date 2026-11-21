using HubPedidos.Application.Contratos;

namespace HubPedidos.Application.Pedidos;

//União fechada dos desfechos possíveis do caso de uso (pattern matching)
public abstract record ResultadoPedido
{
    private ResultadoPedido()
    {
    }

    public sealed record Criado(PedidoResponse Pedido) : ResultadoPedido;

    public sealed record Invalido(IReadOnlyList<string> Erros) : ResultadoPedido;

    public sealed record Conflito(string Mensagem) : ResultadoPedido;
}
