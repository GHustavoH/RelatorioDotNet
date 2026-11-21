using Microsoft.AspNetCore.SignalR;
using RelatorioFinal.Contratos;

namespace RelatorioFinal.TempoReal;

public class NotificadorSignalR(IHubContext<PedidosHub> hub) : INotificadorPedidos
{
    // envia so o evento minimo e nunca o objeto de dominio
    public Task NotificarAsync(PedidoAtualizado evento, CancellationToken ct) =>
        hub.Clients.Group(PedidosHub.NomeGrupo(evento.PedidoId)).SendAsync("PedidoAtualizado", evento, ct);
}
