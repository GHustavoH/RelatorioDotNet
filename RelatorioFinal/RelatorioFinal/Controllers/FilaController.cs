using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RelatorioFinal.Fila;

namespace RelatorioFinal.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/fila")]
public class FilaController(Channel<ProcessarPedidoJob> canal, MetricasFila metricas, IConfiguration config) : ControllerBase
{
    [HttpGet("metricas")]
    public IActionResult Obter() => Ok(new
    {
        profundidade = canal.Reader.Count,
        capacidade = config.GetValue("Fila:Capacidade", 100),
        contadores = metricas.Snapshot()
    });
}
