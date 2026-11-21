using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using RelatorioFinal.Contratos;
using RelatorioFinal.Persistencia;

namespace RelatorioFinal.Controllers;

[ApiController]
[Route("api/estatisticas")]
public class EstatisticasController(AppDbContext db) : ControllerBase
{
    // leitura publica e sem dados pessoais entao pode usar output cache
    [HttpGet]
    [OutputCache(PolicyName = "Estatisticas")]
    public async Task<EstatisticasResponse> Obter(CancellationToken ct) =>
        await ConsultasPedido.EstatisticasAsync(db, ct);
}
