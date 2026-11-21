using Hub.Orders.Application;
using Microsoft.AspNetCore.Mvc;
using RELATORIO006.Contracts;
using RELATORIO006.Versioning;

namespace RELATORIO006.Controllers;

/// <summary>Consulta de pedido - contrato v2 (mudança incompatível em "total").</summary>
[ApiController]
[Route("api/orders")]
[ApiExplorerSettings(GroupName = "v2")]
public sealed class OrdersV2Controller(IOrderService orders) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [MapToApiVersion(2)]
    [ProducesResponseType<OrderV2Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderV2Response>> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);
        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Pedido não encontrado")
            : Ok(order.ToV2());
    }
}
