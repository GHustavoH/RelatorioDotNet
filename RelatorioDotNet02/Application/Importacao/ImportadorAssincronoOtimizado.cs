using System.Buffers;
using System.Text;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// OTIMIZADO assíncrono:
//  - lê o stream em blocos para um char[] alugado do ArrayPool (sem string por linha);
//  - limite total de caracteres + CancellationToken;
//  - log agrupado (StringBuilder) e UM flush no final.
//
// Ciclo de vida do buffer (ArrayPool<char>):
//   Rent -> try { usa só [0..fim) -> não assume que veio zerado } finally { Return }
// Spans só existem dentro de métodos síncronos (ProcessarBloco / ProcessarUltimaLinha),
// por isso nunca atravessam um await.
public sealed class ImportadorAssincronoOtimizado
{
    public const int TamanhoBufferPadrao = 16 * 1024;

    private readonly ArrayPool<char> _pool;
    private readonly int _tamanhoBuffer;
    private readonly bool _limparBuffer;

    // limparBuffer = true: Return(clearArray: true) -> usar quando o lote tiver dado sensível.
    // Custo: zerar o buffer a cada devolução (medido no relatório).
    public ImportadorAssincronoOtimizado(
        ArrayPool<char>? pool = null,
        int tamanhoBuffer = TamanhoBufferPadrao,
        bool limparBuffer = false)
    {
        if (tamanhoBuffer < 64)
        {
            throw new ArgumentOutOfRangeException(nameof(tamanhoBuffer), "Mínimo de 64 caracteres.");
        }

        _pool = pool ?? ArrayPool<char>.Shared;
        _tamanhoBuffer = tamanhoBuffer;
        _limparBuffer = limparBuffer;
    }

    public async Task<ResultadoImportacao> ImportarAsync(
        Stream stream,
        long limiteCaracteres,
        TextWriter? log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var pedidos = new List<Pedido>();
        var erros = new List<string>();
        var sbLog = log is null ? null : new StringBuilder();

        var buffer = _pool.Rent(_tamanhoBuffer);
        try
        {
            using var leitor = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

            var inicio = 0; // início da parte ainda não processada
            var fim = 0;    // fim dos dados válidos (o resto do buffer é lixo)
            var numero = 0;
            long total = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // move o pedaço de linha incompleta para o começo do buffer
                if (inicio > 0)
                {
                    Array.Copy(buffer, inicio, buffer, 0, fim - inicio);
                    fim -= inicio;
                    inicio = 0;
                }

                if (fim >= _tamanhoBuffer)
                {
                    throw new InvalidDataException($"Linha maior que o buffer de {_tamanhoBuffer} caracteres.");
                }

                var lidos = await leitor.ReadAsync(buffer.AsMemory(fim, _tamanhoBuffer - fim), cancellationToken);
                if (lidos == 0)
                {
                    break;
                }

                total += lidos;
                if (total > limiteCaracteres)
                {
                    throw new InvalidDataException($"Lote excede o limite de {limiteCaracteres} caracteres.");
                }

                fim += lidos;
                ProcessarBloco(buffer, ref inicio, fim, ref numero, pedidos, erros, sbLog);

                // grava o log do bloco todo de uma vez (sem flush)
                if (log is not null && sbLog!.Length > 0)
                {
                    await log.WriteAsync(sbLog, cancellationToken);
                    sbLog.Clear();
                }
            }

            // última linha (sem \n no final)
            if (fim > inicio)
            {
                ProcessarUltimaLinha(buffer, inicio, fim, ref numero, pedidos, erros, sbLog);
            }

            if (log is not null)
            {
                if (sbLog!.Length > 0)
                {
                    await log.WriteAsync(sbLog, cancellationToken);
                }

                await log.FlushAsync(cancellationToken); // um único flush
            }

            return new ResultadoImportacao(pedidos, erros);
        }
        finally
        {
            // roda em sucesso, erro e cancelamento; depois disso 'buffer' não é mais usado
            _pool.Return(buffer, clearArray: _limparBuffer);
        }
    }

    private static void ProcessarBloco(
        char[] buffer,
        ref int inicio,
        int fim,
        ref int numero,
        List<Pedido> pedidos,
        List<string> erros,
        StringBuilder? log)
    {
        while (true)
        {
            var janela = buffer.AsSpan(inicio, fim - inicio);
            var quebra = janela.IndexOf('\n');
            if (quebra < 0)
            {
                return;
            }

            numero++;
            var errosAntes = erros.Count;
            var status = ParserLinhaPedido.Processar(janela[..quebra], numero, pedidos, erros);
            if (log is not null)
            {
                FormatoLote.AnexarLog(log, numero, status, erros.Count - errosAntes);
            }

            inicio += quebra + 1;
        }
    }

    private static void ProcessarUltimaLinha(
        char[] buffer,
        int inicio,
        int fim,
        ref int numero,
        List<Pedido> pedidos,
        List<string> erros,
        StringBuilder? log)
    {
        numero++;
        var errosAntes = erros.Count;
        var status = ParserLinhaPedido.Processar(buffer.AsSpan(inicio, fim - inicio), numero, pedidos, erros);
        if (log is not null)
        {
            FormatoLote.AnexarLog(log, numero, status, erros.Count - errosAntes);
        }
    }
}
