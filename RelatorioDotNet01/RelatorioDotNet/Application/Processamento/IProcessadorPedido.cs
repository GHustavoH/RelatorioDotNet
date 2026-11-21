using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Processamento;

/// <summary>Estratégia de processamento. Implementações são descobertas pelo <see cref="CatalogoProcessadores"/>.</summary>
public interface IProcessadorPedido
{
    ResultadoProcessamento Processar(Pedido pedido);
}

public sealed record ResultadoProcessamento(string Descricao, decimal ValorFinal);
