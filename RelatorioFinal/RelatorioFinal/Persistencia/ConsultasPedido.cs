using Microsoft.EntityFrameworkCore;
using RelatorioFinal.Contratos;

namespace RelatorioFinal.Persistencia;

public static class ConsultasPedido
{
    // projecao direta no banco com filtro, ordenacao deterministica, paginacao e contagem total
    public static async Task<Pagina<PedidoResumo>> ListarAsync(
        AppDbContext db, string? cliente, string? status, string? categoria, int pagina, int tamanho, CancellationToken ct)
    {
        IQueryable<PedidoEntidade> consulta = db.Pedidos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(cliente))
        {
            consulta = consulta.Where(p => p.Cliente == cliente);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            consulta = consulta.Where(p => p.Categoria == categoria);
        }

        var total = await consulta.CountAsync(ct);

        var itens = await consulta
            .OrderByDescending(p => p.CriadoEm)
            .ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(p => new PedidoResumo(p.Id, p.Cliente, p.Categoria, p.Status, p.Itens.Count, p.Total, p.CriadoEm))
            .ToListAsync(ct);

        return new Pagina<PedidoResumo>(itens, pagina, tamanho, total);
    }

    // consulta simples sem tracking porque so leitura
    public static Task<PedidoEntidade?> BuscarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        db.Pedidos.AsNoTracking().Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, ct);

    // versao compilada para comparar com a simples, so vale a pena se a medicao mostrar ganho
    public static readonly Func<AppDbContext, Guid, CancellationToken, Task<PedidoEntidade?>> BuscarCompilada =
        EF.CompileAsyncQuery((AppDbContext db, Guid id) =>
            db.Pedidos.AsNoTracking().Include(p => p.Itens).FirstOrDefault(p => p.Id == id));

    public static async Task<EstatisticasResponse> EstatisticasAsync(AppDbContext db, CancellationToken ct)
    {
        var porStatus = await db.Pedidos.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new EstatisticaItem(g.Key, g.Count()))
            .ToListAsync(ct);

        var porCategoria = await db.Pedidos.AsNoTracking()
            .GroupBy(p => p.Categoria)
            .Select(g => new EstatisticaItem(g.Key, g.Count()))
            .ToListAsync(ct);

        return new EstatisticasResponse(porStatus, porCategoria);
    }
}
