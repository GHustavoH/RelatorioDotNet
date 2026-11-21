using System.Diagnostics.CodeAnalysis;
using HubPedidos.Domain.Comum;

namespace HubPedidos.Domain.Pedidos;

public sealed record PedidoId : IParsable<PedidoId>
{
    public Guid Valor { get; }

    private PedidoId(Guid valor) => Valor = valor;

    public static PedidoId Novo() => new(Guid.NewGuid());

    public static Resultado<PedidoId> Criar(Guid valor) =>
        valor == Guid.Empty
            ? Resultado<PedidoId>.Falha("O identificador do pedido não pode ser vazio.")
            : Resultado<PedidoId>.Ok(new PedidoId(valor));

    public static PedidoId Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var resultado)
            ? resultado
            : throw new FormatException("Identificador de pedido inválido: esperado GUID não vazio.");

    public static bool TryParse(
        [NotNullWhen(true)] string? s,
        IFormatProvider? provider,
        [MaybeNullWhen(false)] out PedidoId result)
    {
        return TryParse(s is null ? default : s.AsSpan(), out result);
    }

    // Roteiro 2: versão sem alocação (não cria string para o recorte nem para o Trim).
    public static bool TryParse(ReadOnlySpan<char> s, [MaybeNullWhen(false)] out PedidoId result)
    {
        result = null;

        if (Guid.TryParseExact(s.Trim(), "D", out var guid) && guid != Guid.Empty)
        {
            result = new PedidoId(guid);
            return true;
        }

        return false;
    }

    public override string ToString() => Valor.ToString("D");
}
