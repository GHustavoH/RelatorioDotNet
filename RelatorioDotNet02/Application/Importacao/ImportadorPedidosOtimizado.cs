using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// Versão síncrona otimizada: percorre o texto inteiro com spans (sem Split de linhas).
public static class ImportadorPedidosOtimizado
{
    public static ResultadoImportacao Importar(string conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        return Importar(conteudo.AsSpan());
    }

    public static ResultadoImportacao Importar(ReadOnlySpan<char> conteudo)
    {
        var pedidos = new List<Pedido>();
        var erros = new List<string>();

        var numero = 0;
        while (true)
        {
            numero++;
            var fim = conteudo.IndexOf('\n');
            var linha = fim < 0 ? conteudo : conteudo[..fim];

            ParserLinhaPedido.Processar(linha, numero, pedidos, erros);

            if (fim < 0)
            {
                break;
            }

            conteudo = conteudo[(fim + 1)..];
        }

        return new ResultadoImportacao(pedidos, erros);
    }
}
