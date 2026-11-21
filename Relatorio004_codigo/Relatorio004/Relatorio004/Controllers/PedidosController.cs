using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Relatorio004.Models;
using Relatorio004.Services;

namespace Relatorio004.Controllers;

[ApiController]
[Route("pedidos")]
public class PedidosController : ControllerBase
{
    private readonly Channel<ProcessarPedidoJob> _canal;
    private readonly Metricas _metricas;
    private readonly int _capacidade;

    public PedidosController(Channel<ProcessarPedidoJob> canal, Metricas metricas, IConfiguration config)
    {
        _canal = canal;
        _metricas = metricas;
        _capacidade = config.GetValue<int>("Fila:Capacidade", 10);
    }

    // rejeicao explicita com TryWrite
    // 202 = entrou na fila 429 = fila cheia 503 = fila fechada (aplicacao parando)
    // obs: nos modos de descarte o TryWrite sempre devolve true, a perda aparece em "descartados"
    [HttpPost]
    public IActionResult Criar(PedidoRequest request)
    {
        var job = new ProcessarPedidoJob(Guid.NewGuid(), request.Cliente, request.Valor, DateTime.UtcNow);

        if (_canal.Writer.TryWrite(job))
        {
            _metricas.Somar("aceitos");
            return Accepted(new { job.Id });
        }

        if (_canal.Reader.Completion.IsCompleted)
        {
            _metricas.Somar("rejeitadosShutdown");
            return StatusCode(503, "Aplicacao encerrando, fila fechada");
        }

        _metricas.Somar("rejeitados");
        return StatusCode(429, "Fila cheia, tente novamente depois");
    }

    // espera abrir vaga na fila (FullMode Wait), respeitando o cancelamento
    [HttpPost("aguardar")]
    public async Task<IActionResult> CriarAguardando(PedidoRequest request, CancellationToken ct)
    {
        var job = new ProcessarPedidoJob(Guid.NewGuid(), request.Cliente, request.Valor, DateTime.UtcNow);

        try
        {
            await _canal.Writer.WriteAsync(job, ct);
            _metricas.Somar("aceitos");
            return Accepted(new { job.Id });
        }
        catch (OperationCanceledException)
        {
            _metricas.Somar("cancelados");
            return StatusCode(499);
        }
        catch (ChannelClosedException)
        {
            _metricas.Somar("rejeitadosShutdown");
            return StatusCode(503, "Aplicacao encerrando, fila fechada");
        }
    }

    // gera carga acima da capacidade com taxa controlada
    [HttpPost("carga")]
    public async Task<IActionResult> Carga(int quantidade = 50, int intervaloMs = 20, CancellationToken ct = default)
    {
        for (int i = 0; i < quantidade; i++)
        {
            var job = new ProcessarPedidoJob(Guid.NewGuid(), "cliente" + i, 10 + i, DateTime.UtcNow);

            if (_canal.Writer.TryWrite(job))
                _metricas.Somar("aceitos");
            else
                _metricas.Somar("rejeitados");

            _metricas.Somar("profundidadeSoma", _canal.Reader.Count);
            _metricas.Somar("profundidadeAmostras");

            await Task.Delay(intervaloMs, ct);
        }

        return Ok(new { enviados = quantidade });
    }

    [HttpGet("metricas")]
    public IActionResult Metricas()
    {
        var m = _metricas.Todas();
        long lidos = m.GetValueOrDefault("processados") + m.GetValueOrDefault("falhas");
        long espera = m.GetValueOrDefault("esperaTotalMs");
        long somaProf = m.GetValueOrDefault("profundidadeSoma");
        long amostras = m.GetValueOrDefault("profundidadeAmostras");

        return Ok(new
        {
            capacidade = _capacidade,
            profundidadeAtual = _canal.Reader.Count,
            profundidadeMedia = amostras == 0 ? 0 : somaProf / amostras,
            esperaMediaMs = lidos == 0 ? 0 : espera / lidos,
            aceitos = m.GetValueOrDefault("aceitos"),
            processados = m.GetValueOrDefault("processados"),
            descartados = m.GetValueOrDefault("descartados"),
            rejeitados = m.GetValueOrDefault("rejeitados"),
            falhas = m.GetValueOrDefault("falhas"),
            interrompidos = m.GetValueOrDefault("interrompidos"),
            pendentesNaoProcessados = m.GetValueOrDefault("pendentesNaoProcessados"),
            maxConcorrenciaBanco = m.GetValueOrDefault("maxConcorrenciaBanco")
        });
    }
}
