using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Relatorio005.Hub;

namespace Relatorio005.Controllers;

[ApiController]
[Route("api/diagnostico")]
public sealed class DiagnosticoController(
    FilaMensagens fila,
    MensagemStore store,
    IOptions<HubOptions> opcoes) : ControllerBase
{
    [HttpGet("estado")]
    public IActionResult Estado()
    {
        var o = opcoes.Value;

        return Ok(new
        {
            estrategia = o.Estrategia.ToString(),
            workers = o.Workers,
            capacidadeFila = fila.Capacidade,
            profundidadeFila = fila.Profundidade,
            mensagensArmazenadas = store.Total,
            capacidadeArmazenamento = store.Capacidade,
            seedQuantidade = o.SeedQuantidade,
            seedAleatoria = o.SeedAleatoria,
            latenciaPersistenciaMs = o.LatenciaPersistenciaMs,
            iteracoesHash = o.IteracoesHash,
            banco = "memoria (array em processo)",
            compilacao = Compilacao(),
            runtime = RuntimeInformation.FrameworkDescription,
            sistemaOperacional = RuntimeInformation.OSDescription,
            processId = Environment.ProcessId,
            gcServidor = System.Runtime.GCSettings.IsServerGC,
            threadPoolThreads = ThreadPool.ThreadCount,
            threadPoolPendentes = ThreadPool.PendingWorkItemCount
        });
    }

    private static string Compilacao()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
