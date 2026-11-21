using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using relatorio007.Models;
using relatorio007.Services;

namespace relatorio007.Hubs;

[Authorize]
public class PedidosHub : Hub<IPedidosCliente>
{
    private readonly PedidoStore _store;
    private readonly Assinaturas _assinaturas;

    public PedidosHub(PedidoStore store, Assinaturas assinaturas)
    {
        _store = store;
        _assinaturas = assinaturas;
    }

    public static string NomeGrupo(int pedidoId) => $"pedido-{pedidoId}";

    // a autorizacao e conferida no servidor antes de entrar no grupo
    public async Task Entrar(int pedidoId)
    {
        var usuario = Context.UserIdentifier;
        var pedido = _store.Obter(pedidoId);

        var permitido = pedido != null && usuario != null &&
            (pedido.DonoId == usuario || Context.User!.IsInRole("admin"));

        if (!permitido)
            throw new HubException("Acesso negado");

        await Groups.AddToGroupAsync(Context.ConnectionId, NomeGrupo(pedidoId));
        _assinaturas.Adicionar(Context.ConnectionId, usuario!, pedidoId);
    }

    public async Task Sair(int pedidoId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, NomeGrupo(pedidoId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _assinaturas.RemoverConexao(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
