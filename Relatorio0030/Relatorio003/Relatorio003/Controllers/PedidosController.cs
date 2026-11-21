using Microsoft.AspNetCore.Mvc;
using Relatorio003.Consultas;
using Relatorio003.Dominio;
using Relatorio003.Servicos;

namespace Relatorio003.Controllers;

public sealed record AtualizarStatusRequest(StatusPedido Status);

[ApiController]
[Route("api/pedidos")]
public class PedidosController(
    PedidoPainelService painel,
    PedidoRelatorioService relatorio,
    PedidoComandoService comandos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginaResultado<PedidoResumoDto>>> Listar(
        [FromQuery] StatusPedido? status, [FromQuery] string? cidade, [FromQuery] string? cliente,
        [FromQuery] DateTimeOffset? de, [FromQuery] DateTimeOffset? ate,
        [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20, CancellationToken ct = default)
    {
        var filtro = new FiltroPedidos(status, cidade, cliente, de, ate);
        return await painel.ListarAsync(filtro, pagina, tamanho, ct);
    }

    [HttpGet("relatorio")]
    public async Task<ActionResult<List<PedidoRelatorioDto>>> Relatorio(
        [FromQuery] EstrategiaRelatorio estrategia = EstrategiaRelatorio.Projecao,
        [FromQuery] int pagina = 1, [FromQuery] int tamanho = 20, CancellationToken ct = default) =>
        await relatorio.ObterPaginaAsync(estrategia, pagina, tamanho, ct);

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> AtualizarStatus(int id, [FromBody] AtualizarStatusRequest req, CancellationToken ct) =>
        await comandos.AtualizarStatusAsync(id, req.Status, ct) ? NoContent() : NotFound();
}
