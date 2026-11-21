using System.Globalization;
using System.Text;

namespace HubPedidos.Application.Importacao;

// Formato delimitado (uma linha por pedido):
//   id;regiao;prioridade;sku:quantidade:preco|sku:quantidade:preco
// Exemplo:
//   00000001-0000-0000-0000-000000000000;Sul;Alta;ABC:2:10.50|XYZ:1:5.00
// Linhas separadas por \n (ou \r\n). Linhas em branco são ignoradas.
// Baseline e versão otimizada usam EXATAMENTE as mesmas regras abaixo.
public static class FormatoLote
{
    public static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    public const NumberStyles EstiloInteiro =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowLeadingSign;

    public const NumberStyles EstiloDecimal =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
        | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public const string ErroCampos = "esperado 4 campos separados por ';'.";
    public const string ErroItemFormato = "formato inválido (esperado sku:quantidade:preco).";
    public const string ErroId = "id: formato inválido (esperado GUID não vazio).";

    // Linha de log usada pelos importadores assíncronos (mesmo texto nos dois).
    public static void AnexarLog(StringBuilder sb, int numero, StatusLinha status, int quantidadeErros)
    {
        if (status == StatusLinha.Vazia)
        {
            return;
        }

        sb.Append("linha ").Append(numero).Append(": ");
        if (status == StatusLinha.Ok)
        {
            sb.Append("OK");
        }
        else
        {
            sb.Append("ERRO (").Append(quantidadeErros).Append(')');
        }

        sb.Append('\n');
    }
}
