using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v2/pedidos")]
public class PedidosV2Controller : ControllerBase
{
    private readonly AppDbContext _db;

    public PedidosV2Controller(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoV2>> Obter(int id)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido == null)
        {
            return NotFound();
        }

        // etag = hash dos dados do pedido
        var texto = $"{pedido.Id}|{pedido.Cliente}|{pedido.Valor}|{pedido.Status}|{pedido.CriadoEm:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
        var etag = $"\"{hash}\"";

        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch.Any(x => x == etag))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return new PedidoV2(pedido.Id, pedido.Cliente, pedido.Valor, pedido.Status, pedido.CriadoEm);
    }
}
