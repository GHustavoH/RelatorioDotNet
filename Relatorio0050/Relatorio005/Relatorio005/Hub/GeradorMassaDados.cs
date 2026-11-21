namespace Relatorio005.Hub;

public static class GeradorMassaDados
{
    public static readonly string[] Canais =
    {
        "vendas", "suporte", "financeiro", "logistica", "marketing", "rh", "ti", "juridico"
    };

    public static readonly string[] Vocabulario =
    {
        "pedido", "entrega", "fatura", "cliente", "contrato", "pagamento", "estoque", "relatorio",
        "chamado", "reembolso", "cancelamento", "atualizacao", "prazo", "cobranca", "produto",
        "endereco", "nota", "fiscal", "troca", "devolucao", "analise", "aprovacao", "revisao",
        "orcamento", "proposta", "reuniao", "sistema", "acesso", "senha", "ticket"
    };

    public static Mensagem[] Gerar(int quantidade, int semente)
    {
        var aleatorio = new Random(semente);
        var baseTempo = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var massa = new Mensagem[quantidade];
        var bytesId = new byte[16];

        for (var i = 0; i < quantidade; i++)
        {
            aleatorio.NextBytes(bytesId);
            var palavras = aleatorio.Next(8, 15);
            var conteudo = string.Join(' ', Enumerable.Range(0, palavras).Select(_ => Vocabulario[aleatorio.Next(Vocabulario.Length)]));

            massa[i] = new Mensagem(
                new Guid(bytesId),
                i + 1,
                Canais[aleatorio.Next(Canais.Length)],
                conteudo,
                aleatorio.Next(0, 10),
                baseTempo.AddSeconds(i));
        }

        return massa;
    }
}
