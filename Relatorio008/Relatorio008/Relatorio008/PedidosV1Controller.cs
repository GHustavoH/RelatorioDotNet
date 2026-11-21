using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1/pedidos")]
public class PedidosV1Controller : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFilaPedidos _fila;

    public PedidosV1Controller(AppDbContext db, IFilaPedidos fila)
    {
        _db = db;
        _fila = fila;
    }

    [HttpPost]
    public async Task<ActionResult<PedidoV1>> Criar(CriarPedidoRequest request)
    {
        var pedido = new Pedido
        {
            Cliente = request.Cliente,
            Valor = request.Valor,
            CriadoEm = DateTime.UtcNow
        };

        _db.Pedidos.Add(pedido);
        await _db.SaveChangesAsync();

        // so publica depois de salvar no banco
        await _fila.PublicarAsync(new PedidoCriado(pedido.Id, pedido.Cliente, pedido.Valor));

        var resposta = new PedidoV1(pedido.Id, pedido.Cliente, pedido.Valor);
        return CreatedAtAction(nameof(Obter), new { id = pedido.Id }, resposta);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoV1>> Obter(int id)
    {
        var pedido = await _db.Pedidos.FindAsync(id);
        if (pedido == null)
        {
            return NotFound();
        }

        return new PedidoV1(pedido.Id, pedido.Cliente, pedido.Valor);
    }
}
