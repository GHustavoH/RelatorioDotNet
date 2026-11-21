using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// Resultado de uma importação de lote: pedidos válidos + erros (com número da linha).
public sealed record ResultadoImportacao(IReadOnlyList<Pedido> Pedidos, IReadOnlyList<string> Erros)
{
    public bool Sucesso => Erros.Count == 0;
}

public enum StatusLinha
{
    Vazia,
    Ok,
    Erro
}
