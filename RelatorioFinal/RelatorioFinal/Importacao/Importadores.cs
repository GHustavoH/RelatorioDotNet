using System.Buffers;
using System.Globalization;
using System.Text;

namespace RelatorioFinal.Importacao;

// cada linha traz cliente, regiao, prioridade, produto, quantidade e preco separados por ponto e virgula
public record LinhaImportada(string Cliente, string Regiao, string Prioridade, string Produto, int Quantidade, decimal Preco);

public static class ImportadorBaseline
{
    // versao simples com Split e Parse que serve de referencia
    public static List<LinhaImportada> Importar(string texto)
    {
        var lista = new List<LinhaImportada>();
        var linhas = texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int i = 0; i < linhas.Length; i++)
        {
            var partes = linhas[i].Split(';');
            if (partes.Length != 6)
            {
                throw new FormatException($"Linha {i + 1} invalida");
            }

            lista.Add(new LinhaImportada(
                partes[0].Trim(),
                partes[1].Trim(),
                partes[2].Trim(),
                partes[3].Trim(),
                int.Parse(partes[4].Trim(), CultureInfo.InvariantCulture),
                decimal.Parse(partes[5].Trim(), CultureInfo.InvariantCulture)));
        }

        return lista;
    }
}

public static class ImportadorSpan
{
    // versao com spans que evita criar strings intermediarias para os numeros
    public static List<LinhaImportada> Importar(ReadOnlySpan<char> texto)
    {
        var lista = new List<LinhaImportada>();
        Span<Range> partes = stackalloc Range[7];
        int numero = 0;

        foreach (var linhaBruta in texto.EnumerateLines())
        {
            var linha = linhaBruta.Trim();
            if (linha.IsEmpty)
            {
                continue;
            }

            numero++;
            int quantidade = linha.Split(partes, ';');
            if (quantidade != 6)
            {
                throw new FormatException($"Linha {numero} invalida");
            }

            lista.Add(new LinhaImportada(
                linha[partes[0]].Trim().ToString(),
                linha[partes[1]].Trim().ToString(),
                linha[partes[2]].Trim().ToString(),
                linha[partes[3]].Trim().ToString(),
                int.Parse(linha[partes[4]].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture),
                decimal.Parse(linha[partes[5]].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture)));
        }

        return lista;
    }
}

public static class ImportadorStream
{
    // le o stream com limite total e buffers alugados do pool
    public static async Task<List<LinhaImportada>> ImportarAsync(Stream stream, int limiteBytes, CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(limiteBytes + 1);
        try
        {
            int total = 0;
            while (true)
            {
                int lidos = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
                if (lidos == 0)
                {
                    break;
                }

                total += lidos;
                if (total > limiteBytes)
                {
                    throw new InvalidDataException($"O arquivo passou do limite de {limiteBytes} bytes");
                }
            }

            return ImportarBytes(buffer.AsSpan(0, total));
        }
        finally
        {
            // limpa porque o conteudo pode ser sensivel e devolve sempre
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    // metodo sincrono porque spans nao podem atravessar await
    private static List<LinhaImportada> ImportarBytes(ReadOnlySpan<byte> bytes)
    {
        var caracteres = ArrayPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(bytes.Length));
        try
        {
            int quantidade = Encoding.UTF8.GetChars(bytes, caracteres);
            return ImportadorSpan.Importar(caracteres.AsSpan(0, quantidade));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(caracteres, clearArray: true);
        }
    }
}
