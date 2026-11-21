using Microsoft.EntityFrameworkCore;
using Relatorio003.Dados;
using Relatorio003.Dominio;

namespace Relatorio003.Consultas;

// Caso repetitivo e estável: pedidos de um status, paginados, com parâmetros escalares.
// A versão compilada vale para UM único modelo (PedidosDbContext) e não aceita composição
//dinâmica (filtros opcionais). As duas expressões precisam ser mantidas idênticas.

public static class PedidoConsultas
{
    public static IQueryable<PedidoResumoDto> PorStatus(PedidosDbContext db, StatusPedido status, int pular, int tamanho) =>
        db.Pedidos.AsNoTracking()
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
            .Skip(pular).Take(tamanho)
            .Select(p => new PedidoResumoDto(
                p.Id, p.Numero, p.Cliente, p.Status, p.CriadoEm,
                p.EnderecoEntrega.Cidade, p.Itens.Count, p.Total));

    public static readonly Func<PedidosDbContext, StatusPedido, int, int, IAsyncEnumerable<PedidoResumoDto>> PorStatusCompilada =
        EF.CompileAsyncQuery((PedidosDbContext db, StatusPedido status, int pular, int tamanho) =>
            db.Pedidos.AsNoTracking()
                .Where(p => p.Status == status)
                .OrderByDescending(p => p.CriadoEm).ThenByDescending(p => p.Id)
                .Skip(pular).Take(tamanho)
                .Select(p => new PedidoResumoDto(
                    p.Id, p.Numero, p.Cliente, p.Status, p.CriadoEm,
                    p.EnderecoEntrega.Cidade, p.Itens.Count, p.Total)));
}
