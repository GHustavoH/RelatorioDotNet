using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Relatorio005.Hub;
using Relatorio005.Models;

namespace Relatorio005.Controllers;

[ApiController]
[Route("api/mensagens")]
public sealed class MensagensController(
    FilaMensagens fila,
    MensagemStore store,
    HubMetricas metricas,
    IOptions<HubOptions> opcoes) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(RespostaCriacao), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Criar([FromBody] CriarMensagemRequest requisicao)
    {
        var id = Guid.NewGuid();
        var pedido = new PedidoMensagem(
            id,
            requisicao.Canal.Trim(),
            requisicao.Conteudo,
            requisicao.Prioridade,
            Stopwatch.GetTimestamp());

        if (!fila.TentarEnfileirar(pedido))
        {
            Response.Headers.RetryAfter = "1";
            return Problem(title: "Fila cheia", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return AcceptedAtAction(nameof(Obter), new { id }, new RespostaCriacao(id));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Mensagem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Mensagem> Obter(Guid id)
    {
        var mensagem = store.Obter(id);
        return mensagem is null ? NotFound() : Ok(mensagem);
    }

    [HttpGet("busca")]
    [ProducesResponseType(typeof(IReadOnlyList<Mensagem>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<Mensagem>> Buscar(
        [FromQuery, Required, StringLength(30, MinimumLength = 2)] string canal,
        [FromQuery, Required] string termo,
        [FromQuery, Range(1, 100)] int limite = 20)
    {
        var inicio = Stopwatch.GetTimestamp();
        var resultado = BuscaMensagens.Executar(opcoes.Value.Estrategia, store.Itens, store.Total, canal, termo, limite);

        metricas.Buscas.Add(1);
        metricas.DuracaoBuscaMs.Record(Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

        return Ok(resultado);
    }
}
