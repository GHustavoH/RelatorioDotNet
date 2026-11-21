using System.Threading.Channels;

namespace Relatorio008.Tests;

// fila falsa: guarda as mensagens para o teste conferir
public class FilaFalsa : IFilaPedidos
{
    private readonly Channel<PedidoCriado> _canal = Channel.CreateUnbounded<PedidoCriado>();

    public ValueTask PublicarAsync(PedidoCriado mensagem)
    {
        return _canal.Writer.WriteAsync(mensagem);
    }

    // espera a mensagem chegar sem usar Task.Delay.
    // o timeout so existe para o teste nao travar se a mensagem nunca vier.
    public async Task<PedidoCriado> AguardarMensagemAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        return await _canal.Reader.ReadAsync(cts.Token);
    }

    public bool TemMensagem()
    {
        return _canal.Reader.TryPeek(out _);
    }
}
