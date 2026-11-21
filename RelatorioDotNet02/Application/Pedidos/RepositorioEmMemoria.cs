using System.Collections.Concurrent;
using HubPedidos.Application.Contratos;

namespace HubPedidos.Application.Pedidos;

//Implementação pra testes
public sealed class RepositorioEmMemoria : IPedidoRepositorio
{
    private readonly ConcurrentDictionary<string, PedidoResponse> _pedidos = new();

    public Task InicializarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<bool> SalvarAsync(PedidoResponse pedido, CancellationToken cancellationToken) =>
        Task.FromResult(_pedidos.TryAdd(pedido.Id, pedido));
}
