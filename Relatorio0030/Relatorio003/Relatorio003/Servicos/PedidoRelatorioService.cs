using Microsoft.EntityFrameworkCore;
using Relatorio003.Consultas;
using Relatorio003.Dados;
using Relatorio003.Dominio;

namespace Relatorio003.Servicos;

//Relatório de pedidos com itens, sem N+1. Três estratégias comparáveis.
public sealed class PedidoRelatorioService(PedidosDbContext db)
{
    public Task<List<PedidoRelatorioDto>> ObterPaginaAsync(
        EstrategiaRelatorio estrategia, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanhoPagina = Math.Clamp(tamanhoPagina, 1, PedidoPainelService.TamanhoMaximo);
        var pular = (pagina - 1) * tamanhoPagina;

        return estrategia switch
        {
            EstrategiaRelatorio.Projecao => ComProjecaoAsync(pular, tamanhoPagina, ct),
            EstrategiaRelatorio.IncludeUnico => ComIncludeAsync(pular, tamanhoPagina, dividido: false, ct),
            EstrategiaRelatorio.IncludeDividido => ComIncludeAsync(pular, tamanhoPagina, dividido: true, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(estrategia))
        };
    }

    private Task<List<PedidoRelatorioDto>> ComProjecaoAsync(int pular, int tamanho, CancellationToken ct) =>
        db.Pedidos.AsNoTracking()
            .OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
            .Skip(pular).Take(tamanho)
            .Select(p => new PedidoRelatorioDto(
                p.Id, p.Numero, p.Cliente, p.Status, p.Total,
                p.Itens.OrderBy(i => i.Id)
                    .Select(i => new ItemRelatorioDto(i.Produto, i.Quantidade, i.PrecoUnitario, i.Quantidade * i.PrecoUnitario))
                    .ToList()))
            .ToListAsync(ct);

    private async Task<List<PedidoRelatorioDto>> ComIncludeAsync(int pular, int tamanho, bool dividido, CancellationToken ct)
    {
        IQueryable<Pedido> consulta = db.Pedidos.AsNoTracking()
            .OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
            .Skip(pular).Take(tamanho)
            .Include(p => p.Itens.OrderBy(i => i.Id));

        consulta = dividido ? consulta.AsSplitQuery() : consulta.AsSingleQuery();

        var pedidos = await consulta.ToListAsync(ct);

        return pedidos.Select(p => new PedidoRelatorioDto(
            p.Id, p.Numero, p.Cliente, p.Status, p.Total,
            p.Itens.Select(i => new ItemRelatorioDto(i.Produto, i.Quantidade, i.PrecoUnitario, i.Quantidade * i.PrecoUnitario)).ToList()))
            .ToList();
    }
}
