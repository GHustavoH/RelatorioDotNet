namespace Relatorio004.Services;

public interface IPedidoRepositorio
{
    Task SalvarAsync(Guid id, string cliente, decimal valor, CancellationToken ct);
}
