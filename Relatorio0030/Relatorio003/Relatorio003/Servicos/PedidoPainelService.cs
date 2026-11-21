using Microsoft.EntityFrameworkCore;
using Relatorio003.Consultas;
using Relatorio003.Dados;
using Relatorio003.Dominio;

namespace Relatorio003.Servicos;

//Painel de pedidos: somente leitura (AsNoTracking), projeção em DTO, paginação e total.
public sealed class PedidoPainelService(PedidosDbContext db)
{
    public const int TamanhoMaximo = 100;

    public async Task<PaginaResultado<PedidoResumoDto>> ListarAsync(
        FiltroPedidos filtro, int pagina, int tamanhoPagina, CancellationToken ct = default)
    {
        (pagina, tamanhoPagina) = Normalizar(pagina, tamanhoPagina);

        // Comando 1: contagem total (sem ordenação, sem projeção).
        var total = await Filtrar(db.Pedidos.AsNoTracking(), filtro).CountAsync(ct);

        // Comando 2: página projetada.
        var itens = await MontarConsultaPagina(filtro, pagina, tamanhoPagina).ToListAsync(ct);

        return new PaginaResultado<PedidoResumoDto>(itens, pagina, tamanhoPagina, total);
    }

    public IQueryable<PedidoResumoDto> MontarConsultaPagina(FiltroPedidos filtro, int pagina, int tamanhoPagina)
    {
        (pagina, tamanhoPagina) = Normalizar(pagina, tamanhoPagina);
        var pular = (pagina - 1) * tamanhoPagina; // variável local => parâmetro SQL, nunca constante

        return Filtrar(db.Pedidos.AsNoTracking(), filtro)
            .OrderByDescending(p => p.CriadoEm)
            .ThenByDescending(p => p.Id) // desempate: ordenação determinística
            .Skip(pular)
            .Take(tamanhoPagina)
            .Select(p => new PedidoResumoDto(
                p.Id,
                p.Numero,
                p.Cliente,
                p.Status,
                p.CriadoEm,
                p.EnderecoEntrega.Cidade,
                p.Itens.Count,
                p.Total));
    }

   
    internal static IQueryable<Pedido> Filtrar(IQueryable<Pedido> q, FiltroPedidos f)
    {
        if (f.Status is { } status)
            q = q.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(f.Cidade))
        {
            var cidade = f.Cidade.Trim();
            q = q.Where(p => p.EnderecoEntrega.Cidade == cidade);
        }

        if (!string.IsNullOrWhiteSpace(f.Cliente))
        {
            var padrao = $"%{EscaparLike(f.Cliente.Trim())}%";
            q = q.Where(p => EF.Functions.ILike(p.Cliente, padrao, "\\"));
        }

        if (f.De is { } de)
        {
            var deUtc = de.ToUniversalTime();
            q = q.Where(p => p.CriadoEm >= deUtc);
        }

        if (f.Ate is { } ate)
        {
            var ateUtc = ate.ToUniversalTime();
            q = q.Where(p => p.CriadoEm < ateUtc);
        }

        return q;
    }

    private static (int pagina, int tamanho) Normalizar(int pagina, int tamanho) =>
        (Math.Max(1, pagina), Math.Clamp(tamanho, 1, TamanhoMaximo));

    internal static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
