using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.SignalR;
using relatorio007.Hubs;
using relatorio007.Models;
using relatorio007.Services;

namespace relatorio007.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly PedidoStore _store;
    private readonly Assinaturas _assinaturas;
    private readonly IHubContext<PedidosHub, IPedidosCliente> _hub;
    private readonly IOutputCacheStore _cache;

    public PedidosController(PedidoStore store, Assinaturas assinaturas,
        IHubContext<PedidosHub, IPedidosCliente> hub, IOutputCacheStore cache)
    {
        _store = store;
        _assinaturas = assinaturas;
        _hub = hub;
        _cache = cache;
    }

    // consulta autenticada e personalizada: tem ETag e NAO entra no output cache
    [HttpGet("{id}")]
    [Authorize]
    [OutputCache(NoStore = true)]
    public IActionResult Obter(int id)
    {
        var pedido = _store.Obter(id);
        if (pedido == null)
            return NotFound();

        var usuario = User.Identity?.Name;
        if (pedido.DonoId != usuario && !User.IsInRole("admin"))
            return Forbid();

        var dto = new PedidoDto(pedido.Id, pedido.Status, pedido.Total, pedido.Versao);
        var etag = Etag.Gerar(dto);

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, no-cache";

        if (Etag.Confere(Request.Headers.IfNoneMatch.ToString(), etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(dto);
    }

    // leitura publica e idempotente: unica rota com cache
    [HttpGet("{id}/publico")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "PedidoPublico")]
    public IActionResult ObterPublico(int id)
    {
        var pedido = _store.ObterParaLeituraPublica(id);
        if (pedido == null)
            return NotFound();

        return Ok(new PedidoPublicoDto(pedido.Id, pedido.Status));
    }

    [HttpPost("{id}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AlterarStatus(int id, [FromBody] AlterarStatusRequest request)
    {
        // 1. persiste
        var pedido = _store.AlterarStatus(id, request.Status);
        if (pedido == null)
            return NotFound();

        // 2. invalida o cache
        await _cache.EvictByTagAsync("pedidos", default);

        // 3. so depois da mudanca publica o evento, com dados minimos
        var evento = new PedidoAtualizado(pedido.Id, pedido.Versao, pedido.Status, pedido.Atualizado);
        await _hub.Clients.Group(PedidosHub.NomeGrupo(id)).PedidoAtualizado(evento);

        return Ok(evento);
    }

    // revoga a permissao: tira as conexoes do usuario do grupo do pedido
    [HttpPost("{id}/revogar/{usuario}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Revogar(int id, string usuario)
    {
        var conexoes = _assinaturas.RemoverUsuarioDoPedido(usuario, id);

        foreach (var conexao in conexoes)
            await _hub.Groups.RemoveFromGroupAsync(conexao, PedidosHub.NomeGrupo(id));

        return Ok(new { removidas = conexoes.Count });
    }
}
