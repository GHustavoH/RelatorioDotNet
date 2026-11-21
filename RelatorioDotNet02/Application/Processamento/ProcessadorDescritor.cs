using HubPedidos.Domain.Classificacao;

namespace HubPedidos.Application.Processamento;

public sealed record ProcessadorDescritor(CategoriaProcessamento Categoria, int Versao, Type Tipo)
{
    public string Nome => Tipo.Name;
}
