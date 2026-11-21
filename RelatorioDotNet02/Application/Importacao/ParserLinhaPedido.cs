using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// OTIMIZADO (Roteiro 2): mesmo contrato do baseline, mas o recorte dos campos é feito
// com ReadOnlySpan<char> (IndexOf + fatiamento) em vez de Split.
// Regras de segurança dos spans:
//  - só são usados dentro deste método síncrono;
//  - nunca são guardados em campo, nunca atravessam await;
//  - a única string criada por item é o SKU (o domínio guarda string).
public static class ParserLinhaPedido
{
    public static StatusLinha Processar(ReadOnlySpan<char> linha, int numero, List<Pedido> pedidos, List<string> erros)
    {
        var texto = linha.Trim();
        if (texto.IsEmpty)
        {
            return StatusLinha.Vazia;
        }

        // ---- separa os 4 campos por ';' sem criar strings ----
        var i1 = texto.IndexOf(';');
        if (i1 < 0) return ErroCampos(numero, erros);
        var idTxt = texto[..i1];
        var resto = texto[(i1 + 1)..];

        var i2 = resto.IndexOf(';');
        if (i2 < 0) return ErroCampos(numero, erros);
        var regiaoTxt = resto[..i2];
        resto = resto[(i2 + 1)..];

        var i3 = resto.IndexOf(';');
        if (i3 < 0) return ErroCampos(numero, erros);
        var prioridadeTxt = resto[..i3];
        var itensTxt = resto[(i3 + 1)..];

        if (itensTxt.IndexOf(';') >= 0) return ErroCampos(numero, erros);

        // ---- itens: sku:quantidade:preco separados por '|' ----
        var itens = new List<ItemPedido>();
        List<string>? errosFormato = null;
        List<string>? errosItens = null;

        var indice = 0;
        var restoItens = itensTxt;
        while (true)
        {
            var barra = restoItens.IndexOf('|');
            var itemTxt = barra < 0 ? restoItens : restoItens[..barra];

            if (TentarSepararItem(itemTxt, out var skuTxt, out var quantidade, out var preco))
            {
                var resultadoItem = ItemPedido.Criar(skuTxt.Trim().ToString(), quantidade, preco);
                if (resultadoItem.TryObter(out var item))
                {
                    itens.Add(item);
                }
                else
                {
                    errosItens ??= new List<string>();
                    foreach (var e in resultadoItem.Erros)
                    {
                        errosItens.Add($"itens[{indice}].{e}");
                    }
                }
            }
            else
            {
                errosFormato ??= new List<string>();
                errosFormato.Add($"itens[{indice}]: {FormatoLote.ErroItemFormato}");
            }

            if (barra < 0)
            {
                break;
            }

            restoItens = restoItens[(barra + 1)..];
            indice++;
        }

        if (errosFormato is not null)
        {
            foreach (var e in errosFormato)
            {
                erros.Add($"linha {numero}: {e}");
            }

            return StatusLinha.Erro;
        }

        // ---- mesma ordem de erros do MapeadorPedido: id, regiao, prioridade, itens ----
        var antes = erros.Count;

        PedidoId? id = null;
        if (!PedidoId.TryParse(idTxt, out id))
        {
            erros.Add($"linha {numero}: {FormatoLote.ErroId}");
        }

        if (!TentarConverterEnum(regiaoTxt, out Regiao regiao))
        {
            erros.Add($"linha {numero}: regiao: valor inválido. Use: {string.Join(", ", Enum.GetNames<Regiao>())}.");
        }

        if (!TentarConverterEnum(prioridadeTxt, out Prioridade prioridade))
        {
            erros.Add($"linha {numero}: prioridade: valor inválido. Use: {string.Join(", ", Enum.GetNames<Prioridade>())}.");
        }

        if (errosItens is not null)
        {
            foreach (var e in errosItens)
            {
                erros.Add($"linha {numero}: {e}");
            }
        }

        if (erros.Count > antes)
        {
            return StatusLinha.Erro;
        }

        var resultado = Pedido.Criar(id, itens, regiao, prioridade);
        if (resultado.TryObter(out var pedido))
        {
            pedidos.Add(pedido);
            return StatusLinha.Ok;
        }

        foreach (var e in resultado.Erros)
        {
            erros.Add($"linha {numero}: {e}");
        }

        return StatusLinha.Erro;
    }

    private static StatusLinha ErroCampos(int numero, List<string> erros)
    {
        erros.Add($"linha {numero}: {FormatoLote.ErroCampos}");
        return StatusLinha.Erro;
    }

    private static bool TentarSepararItem(
        ReadOnlySpan<char> item,
        out ReadOnlySpan<char> sku,
        out int quantidade,
        out decimal preco)
    {
        sku = default;
        quantidade = 0;
        preco = 0;

        var c1 = item.IndexOf(':');
        if (c1 < 0) return false;

        var resto = item[(c1 + 1)..];
        var c2 = resto.IndexOf(':');
        if (c2 < 0) return false;

        var quantidadeTxt = resto[..c2];
        var precoTxt = resto[(c2 + 1)..];
        if (precoTxt.IndexOf(':') >= 0) return false; // mais de 3 partes

        if (!int.TryParse(quantidadeTxt, FormatoLote.EstiloInteiro, FormatoLote.Cultura, out quantidade)
            || !decimal.TryParse(precoTxt, FormatoLote.EstiloDecimal, FormatoLote.Cultura, out preco))
        {
            return false;
        }

        sku = item[..c1];
        return true;
    }

    // Mesma regra do MapeadorPedido (só aceita nomes, nunca números), mas sobre span.
    private static bool TentarConverterEnum<TEnum>(ReadOnlySpan<char> texto, out TEnum valor)
        where TEnum : struct, Enum
    {
        valor = default;

        var limpo = texto.Trim();
        if (limpo.IsEmpty)
        {
            return false;
        }

        if (char.IsDigit(limpo[0]) || limpo[0] == '-' || limpo[0] == '+')
        {
            return false;
        }

        return Enum.TryParse(limpo, ignoreCase: true, out valor) && Enum.IsDefined(valor);
    }
}
