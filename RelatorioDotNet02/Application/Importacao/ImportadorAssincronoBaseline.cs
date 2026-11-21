using System.Text;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// BASELINE assíncrono: StreamReader.ReadLineAsync (uma string por linha) e
// escrita de log com Flush a CADA linha.
public static class ImportadorAssincronoBaseline
{
    public static async Task<ResultadoImportacao> ImportarAsync(
        Stream stream,
        long limiteCaracteres,
        TextWriter? log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var pedidos = new List<Pedido>();
        var erros = new List<string>();

        using var leitor = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);

        var numero = 0;
        long total = 0;
        string? linha;
        while ((linha = await leitor.ReadLineAsync(cancellationToken)) is not null)
        {
            numero++;
            total += linha.Length + 1;
            if (total > limiteCaracteres)
            {
                throw new InvalidDataException($"Lote excede o limite de {limiteCaracteres} caracteres.");
            }

            var errosAntes = erros.Count;
            var status = ImportadorPedidosBaseline.ProcessarLinha(linha, numero, pedidos, erros);

            if (log is not null && status != StatusLinha.Vazia)
            {
                var sb = new StringBuilder();
                FormatoLote.AnexarLog(sb, numero, status, erros.Count - errosAntes);
                await log.WriteAsync(sb.ToString().AsMemory(), cancellationToken);
                await log.FlushAsync(cancellationToken); // flush por item (o que queremos avaliar)
            }
        }

        return new ResultadoImportacao(pedidos, erros);
    }
}
