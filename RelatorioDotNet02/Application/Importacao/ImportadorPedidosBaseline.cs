using HubPedidos.Application.Contratos;
using HubPedidos.Application.Mapeamento;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Importacao;

// BASELINE (Roteiro 2): implementação simples e correta, com Split/Trim/string.
// Cada Split cria strings e arrays novos -> é o que a versão otimizada tenta evitar.
public static class ImportadorPedidosBaseline
{
    public static ResultadoImportacao Importar(string conteudo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        var pedidos = new List<Pedido>();
        var erros = new List<string>();

        var linhas = conteudo.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            ProcessarLinha(linhas[i], i + 1, pedidos, erros);
        }

        return new ResultadoImportacao(pedidos, erros);
    }

    // Também usado pelo importador assíncrono baseline.
    public static StatusLinha ProcessarLinha(string linha, int numero, List<Pedido> pedidos, List<string> erros)
    {
        var texto = linha.Trim();
        if (texto.Length == 0)
        {
            return StatusLinha.Vazia;
        }

        var campos = texto.Split(';');
        if (campos.Length != 4)
        {
            erros.Add($"linha {numero}: {FormatoLote.ErroCampos}");
            return StatusLinha.Erro;
        }

        var itensTexto = campos[3].Split('|');
        var itens = new List<ItemPedidoRequest>();
        var errosFormato = new List<string>();

        for (var i = 0; i < itensTexto.Length; i++)
        {
            var partes = itensTexto[i].Split(':');
            if (partes.Length == 3
                && int.TryParse(partes[1], FormatoLote.EstiloInteiro, FormatoLote.Cultura, out var quantidade)
                && decimal.TryParse(partes[2], FormatoLote.EstiloDecimal, FormatoLote.Cultura, out var preco))
            {
                itens.Add(new ItemPedidoRequest(partes[0], quantidade, preco));
            }
            else
            {
                errosFormato.Add($"itens[{i}]: {FormatoLote.ErroItemFormato}");
            }
        }

        if (errosFormato.Count > 0)
        {
            foreach (var erro in errosFormato)
            {
                erros.Add($"linha {numero}: {erro}");
            }

            return StatusLinha.Erro;
        }

        // Reaproveita as regras de validação do Relatório 01 (mapeador + domínio).
        var resultado = MapeadorPedido.ParaDominio(new CriarPedidoRequest(campos[0], itens, campos[1], campos[2]));
        if (resultado.TryObter(out var pedido))
        {
            pedidos.Add(pedido);
            return StatusLinha.Ok;
        }

        foreach (var erro in resultado.Erros)
        {
            erros.Add($"linha {numero}: {erro}");
        }

        return StatusLinha.Erro;
    }
}
