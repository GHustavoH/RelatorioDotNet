using Hub.Orders.Application;
using Microsoft.AspNetCore.Mvc;
using RELATORIO006.Contracts;

namespace RELATORIO006.Controllers;

/// <summary>
/// Comando de criação: sem GroupName, portanto aparece nos documentos OpenAPI de v1 e v2
/// e aceita qualquer versão suportada.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await orders.PlaceAsync(new PlaceOrderCommand
        {
            CustomerName = request.CustomerName,
            Amount = request.Amount,
            Currency = request.Currency
        }, cancellationToken);

        return Created($"/api/orders/{id}", new CreateOrderResponse(id));
    }
}
