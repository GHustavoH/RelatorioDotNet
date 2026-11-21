using HubPedidos.Application.Contratos;

namespace HubPedidos.Application.Pedidos;

public interface IPedidoRepositorio
{
    //Cria a estrutura de persistência, se necessário
    Task InicializarAsync(CancellationToken cancellationToken);

    //Retorna false se já existir pedido com o mesmo identificador
    Task<bool> SalvarAsync(PedidoResponse pedido, CancellationToken cancellationToken);
}
