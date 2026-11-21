using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RelatorioFinal.Persistencia;

namespace RelatorioFinal.TempoReal;

[Authorize]
public class PedidosHub(AppDbContext db) : Hub
{
    // o grupo nao e seguranca, a permissao e conferida aqui antes de entrar
    public async Task EntrarNoPedido(Guid pedidoId)
    {
        var usuario = Context.User?.Identity?.Name;
        var admin = Context.User?.IsInRole("admin") == true;

        var dono = await db.Pedidos.AsNoTracking()
            .Where(p => p.Id == pedidoId)
            .Select(p => p.Cliente)
            .FirstOrDefaultAsync();

        if (dono is null || (!admin && dono != usuario))
        {
            throw new HubException("Sem permissao para acompanhar este pedido");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, NomeGrupo(pedidoId));
    }

    public Task SairDoPedido(Guid pedidoId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, NomeGrupo(pedidoId));

    public static string NomeGrupo(Guid pedidoId) => $"pedido-{pedidoId}";
}
