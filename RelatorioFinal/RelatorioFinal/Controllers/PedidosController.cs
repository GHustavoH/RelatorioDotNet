using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelatorioFinal.Contratos;
using RelatorioFinal.Importacao;
using RelatorioFinal.Servicos;

namespace RelatorioFinal.Controllers;

[ApiController]
[Authorize]
public class PedidosController(IPedidoServico servico) : ControllerBase
{
    [HttpPost("api/pedidos")]
    public async Task<IActionResult> Criar(CriarPedidoRequest req, CancellationToken ct)
    {
        // cliente comum so cria pedido em nome dele mesmo
        if (!User.IsInRole("admin"))
        {
            req = req with { Cliente = User.Identity?.Name ?? "" };
        }

        var resultado = await servico.CriarAsync(req, ct);

        if (resultado.FilaCheia)
        {
            Response.Headers["Retry-After"] = "5";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { erro = resultado.Erro });
        }

        if (resultado.Erro is not null)
        {
            return BadRequest(new { erro = resultado.Erro });
        }

        return Accepted($"/api/pedidos/{resultado.Pedido!.Id}", resultado.Pedido);
    }

    [HttpPost("api/pedidos/importar")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Importar(CancellationToken ct)
    {
        List<LinhaImportada> linhas;
        try
        {
            linhas = await ImportadorStream.ImportarAsync(Request.Body, 1_000_000, ct);
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException)
        {
            return BadRequest(new { erro = ex.Message });
        }

        var requests = linhas
            .Select(l => new CriarPedidoRequest(l.Cliente, l.Regiao, l.Prioridade, [new ItemRequest(l.Produto, l.Quantidade, l.Preco)]))
            .ToList();

        var resultados = await servico.CriarLoteAsync(requests, ct);

        var resumo = new ResumoImportacao(
            resultados.Count,
            resultados.Count(r => r.Pedido is not null),
            resultados.Count(r => r.Erro is not null && !r.FilaCheia),
            resultados.Count(r => r.FilaCheia));

        return Ok(resumo);
    }

    [HttpGet("api/pedidos")]
    public async Task<ActionResult<Pagina<PedidoResumo>>> Listar(
        string? status, string? categoria, int pagina = 1, int tamanho = 20, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 100);

        var cliente = User.IsInRole("admin") ? null : User.Identity?.Name;
        return await servico.ListarAsync(cliente, status, categoria, pagina, tamanho, ct);
    }

    // sem versao na rota vale a versao 1 que e a padrao
    [HttpGet("api/pedidos/{id:guid}")]
    [HttpGet("api/v{versao:int}/pedidos/{id:guid}")]
    public async Task<IActionResult> Obter(Guid id, int versao = 1, CancellationToken ct = default)
    {
        if (versao is not (1 or 2))
        {
            return BadRequest(new { erro = $"Versao {versao} nao suportada, use 1 ou 2" });
        }

        var pedido = await servico.ObterAsync(id, ct);
        if (pedido is null || (!User.IsInRole("admin") && pedido.Cliente != User.Identity?.Name))
        {
            return NotFound();
        }

        object corpo;
        if (versao == 1)
        {
            corpo = PedidoMapeador.ParaV1(pedido);

            // v1 continua funcionando mas ja avisa que esta depreciada
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Sunset"] = "Fri, 31 Dec 2027 23:59:59 GMT";
        }
        else
        {
            corpo = PedidoMapeador.ParaV2(pedido);
        }

        var etag = EtagHelper.Gerar(corpo);
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, must-revalidate";

        if (EtagHelper.Confere(Request.Headers.IfNoneMatch, etag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(corpo);
    }
}
