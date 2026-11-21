namespace Relatorio005.Hub;

public static class BuscaMensagens
{
    public static IReadOnlyList<Mensagem> Executar(
        EstrategiaBusca estrategia,
        Mensagem?[] itens,
        int total,
        string canal,
        string termo,
        int limite) =>
        estrategia == EstrategiaBusca.Otimizada
            ? Otimizada(itens, total, canal, termo, limite)
            : Baseline(itens, total, canal, termo, limite);

    public static IReadOnlyList<Mensagem> Baseline(Mensagem?[] itens, int total, string canal, string termo, int limite)
    {
        return itens
            .Take(total)
            .Where(m => m is not null)
            .Where(m => m!.Canal.ToLowerInvariant() == canal.ToLowerInvariant())
            .Where(m => m!.Conteudo.ToLowerInvariant().Contains(termo.ToLowerInvariant()))
            .OrderByDescending(m => m!.Prioridade)
            .ThenByDescending(m => m!.Sequencia)
            .Select(m => m!)
            .ToList()
            .Take(limite)
            .ToList();
    }

    public static IReadOnlyList<Mensagem> Otimizada(Mensagem?[] itens, int total, string canal, string termo, int limite)
    {
        if (limite <= 0)
        {
            return Array.Empty<Mensagem>();
        }

        var topo = new Mensagem[limite];
        var quantidade = 0;
        var fim = Math.Min(total, itens.Length);

        for (var i = 0; i < fim; i++)
        {
            var mensagem = itens[i];
            if (mensagem is null)
            {
                continue;
            }

            if (!mensagem.Canal.Equals(canal, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (quantidade == limite && !MelhorQue(mensagem, topo[quantidade - 1]))
            {
                continue;
            }

            if (mensagem.Conteudo.IndexOf(termo, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            var posicao = quantidade < limite ? quantidade : limite - 1;
            while (posicao > 0 && MelhorQue(mensagem, topo[posicao - 1]))
            {
                topo[posicao] = topo[posicao - 1];
                posicao--;
            }

            topo[posicao] = mensagem;
            if (quantidade < limite)
            {
                quantidade++;
            }
        }

        return quantidade == limite ? topo : topo.AsSpan(0, quantidade).ToArray();
    }

    private static bool MelhorQue(Mensagem a, Mensagem b) =>
        a.Prioridade > b.Prioridade || (a.Prioridade == b.Prioridade && a.Sequencia > b.Sequencia);
}
