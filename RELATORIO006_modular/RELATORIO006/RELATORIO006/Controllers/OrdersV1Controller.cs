using Hub.Orders.Application;
using Microsoft.AspNetCore.Mvc;
using RELATORIO006.Contracts;
using RELATORIO006.Versioning;

namespace RELATORIO006.Controllers;

/// <summary>Consulta de pedido - contrato v1 (padrão quando a versão é omitida). DEPRECIADO.</summary>
[ApiController]
[Route("api/orders")]
[ApiExplorerSettings(GroupName = "v1")]
public sealed class OrdersV1Controller(IOrderService orders) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [MapToApiVersion(1)]
    [Obsolete("v1 está depreciada. Migre para v2 (ver docs/api/lifecycle.md).")]
    [ProducesResponseType<OrderV1Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderV1Response>> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await orders.GetAsync(id, cancellationToken);
        return order is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Pedido não encontrado")
            : Ok(order.ToV1());
    }
}
