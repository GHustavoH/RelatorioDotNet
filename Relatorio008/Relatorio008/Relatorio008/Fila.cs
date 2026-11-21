using System.Threading.Channels;

public interface IFilaPedidos
{
    ValueTask PublicarAsync(PedidoCriado mensagem);
}

// fila simples em memoria, usada fora dos testes
public class FilaEmMemoria : IFilaPedidos
{
    private readonly Channel<PedidoCriado> _canal = Channel.CreateUnbounded<PedidoCriado>();

    public ChannelReader<PedidoCriado> Leitor => _canal.Reader;

    public ValueTask PublicarAsync(PedidoCriado mensagem)
    {
        return _canal.Writer.WriteAsync(mensagem);
    }
}

// consumidor que so escreve no log
public class ConsumidorPedidos : BackgroundService
{
    private readonly FilaEmMemoria _fila;
    private readonly ILogger<ConsumidorPedidos> _logger;

    public ConsumidorPedidos(FilaEmMemoria fila, ILogger<ConsumidorPedidos> logger)
    {
        _fila = fila;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in _fila.Leitor.ReadAllAsync(stoppingToken))
        {
            _logger.LogInformation("Pedido {Id} recebido na fila", msg.Id);
        }
    }
}
