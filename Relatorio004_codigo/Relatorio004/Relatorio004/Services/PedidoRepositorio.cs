namespace Relatorio004.Services;

// simula o banco de dados (sem EF para o relatorio ficar simples)
public class PedidoRepositorio : IPedidoRepositorio
{
    private readonly int _tempoMs;

    public PedidoRepositorio(IConfiguration config)
    {
        _tempoMs = config.GetValue<int>("Fila:TempoBancoMs", 200);
    }

    public async Task SalvarAsync(Guid id, string cliente, decimal valor, CancellationToken ct)
    {
        await Task.Delay(_tempoMs, ct);

        if (valor <= 0)
        {
            throw new InvalidOperationException("Valor do pedido invalido");
        }
    }
}
