using relatorio007.Models;

namespace relatorio007.Hubs;

public interface IPedidosCliente
{
    Task PedidoAtualizado(PedidoAtualizado evento);
}
