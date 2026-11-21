using System.Collections.Immutable;
using HubPedidos.Application.Contratos;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Comum;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Mapeamento;

public static class MapeadorPedido
{
    public static Resultado<Pedido> ParaDominio(CriarPedidoRequest? request)
    {
        if (request is null)
        {
            return Resultado<Pedido>.Falha("pedido: o corpo da requisição é obrigatório.");
        }

        var erros = new List<string>();

        PedidoId? id = null;
        if (request.Id is null)
        {
            id = PedidoId.Novo();
        }
        else if (PedidoId.TryParse(request.Id, null, out var idConvertido))
        {
            id = idConvertido;
        }
        else
        {
            erros.Add("id: formato inválido (esperado GUID não vazio).");
        }

        if (!TentarConverterEnum(request.Regiao, out Regiao regiao))
        {
            erros.Add($"regiao: valor inválido. Use: {string.Join(", ", Enum.GetNames<Regiao>())}.");
        }

        if (!TentarConverterEnum(request.Prioridade, out Prioridade prioridade))
        {
            erros.Add($"prioridade: valor inválido. Use: {string.Join(", ", Enum.GetNames<Prioridade>())}.");
        }

        var itens = new List<ItemPedido>();
        if (request.Itens is null || request.Itens.Count == 0)
        {
            erros.Add("itens: informe ao menos um item.");
        }
        else
        {
            for (var i = 0; i < request.Itens.Count; i++)
            {
                var item = request.Itens[i];
                if (item is null)
                {
                    erros.Add($"itens[{i}]: não pode ser nulo.");
                    continue;
                }

                var resultadoItem = ItemPedido.Criar(item.Sku, item.Quantidade, item.PrecoUnitario);
                if (resultadoItem.TryObter(out var itemConvertido))
                {
                    itens.Add(itemConvertido);
                }
                else
                {
                    erros.AddRange(resultadoItem.Erros.Select(e => $"itens[{i}].{e}"));
                }
            }
        }

        if (erros.Count > 0)
        {
            return Resultado<Pedido>.Falha(erros);
        }

        return Pedido.Criar(id, itens, regiao, prioridade);
    }

    public static PedidoResponse ParaResponse(
        Pedido pedido,
        CategoriaProcessamento categoria,
        ProcessadorDescritor processador,
        ResultadoProcessamento resultado)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(processador);
        ArgumentNullException.ThrowIfNull(resultado);

        return new PedidoResponse
        {
            Id = pedido.Id.ToString(),
            Itens = pedido.Itens
                .Select(i => new ItemPedidoResponse(i.Sku, i.Quantidade, i.PrecoUnitario, i.Subtotal))
                .ToImmutableArray(),
            QuantidadeItens = pedido.QuantidadeItens,
            ValorTotal = pedido.ValorTotal,
            Regiao = pedido.Regiao.ToString(),
            Prioridade = pedido.Prioridade.ToString(),
            Categoria = categoria.ToString(),
            Processador = processador.Nome,
            VersaoProcessador = processador.Versao,
            DescricaoProcessamento = resultado.Descricao,
            ValorFinal = resultado.ValorFinal
        };
    }

    private static bool TentarConverterEnum<TEnum>(string? texto, out TEnum valor)
        where TEnum : struct, Enum
    {
        valor = default;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpo = texto.Trim();

        // Enum.TryParse aceita 2 ou 99 e rejeita números para só aceitar nomes.
        if (char.IsDigit(limpo[0]) || limpo[0] == '-' || limpo[0] == '+')
        {
            return false;
        }

        return Enum.TryParse(limpo, ignoreCase: true, out valor) && Enum.IsDefined(valor);
    }
}
