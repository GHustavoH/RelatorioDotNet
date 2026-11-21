using HubPedidos.Domain.Classificacao;

namespace HubPedidos.Application.Processamento;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ProcessadorPedidoAttribute : Attribute
{
    public ProcessadorPedidoAttribute(CategoriaProcessamento categoria, int versao)
    {
        Categoria = categoria;
        Versao = versao;
    }

    public CategoriaProcessamento Categoria { get; }

    public int Versao { get; }
}
