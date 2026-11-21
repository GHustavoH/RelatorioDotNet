using Microsoft.EntityFrameworkCore;
using Relatorio003.Dados;
using Relatorio003.Dominio;

namespace Relatorio003.Servicos;

// A regra de tracking está documentada em cada método
public sealed class PedidoComandoService(PedidosDbContext db)
{

    // a entidade é alterada pelo MESMO DbContext, então a consulta usa tracking
    // (SELECT + UPDATE somente da coluna alterada).
    
    public async Task<bool> AtualizarStatusAsync(int id, StatusPedido novoStatus, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct); // tracking (padrão)
        if (pedido is null) return false;

        pedido.Status = novoStatus;
        await db.SaveChangesAsync(ct);
        return true;
    }

    //Caminho no-tracking com anexação explícita quando o dado veio de uma consulta AsNoTracking
    //(ex.: painel), só o Id é conhecido. Anexa uma entidade stub e marca APENAS Status como modificada
    // (1 comando UPDATE, sem SELECT). Retorna false se o pedido não existir (0 linhas afetadas).
   

    public async Task<bool> AtualizarStatusAnexandoAsync(int id, StatusPedido novoStatus, CancellationToken ct = default)
    {
        var stub = new Pedido { Id = id };
        db.Attach(stub);

        var entrada = db.Entry(stub);
        entrada.Property(p => p.Status).CurrentValue = novoStatus;
        entrada.Property(p => p.Status).IsModified = true;

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(stub).State = EntityState.Detached;
            return false;
        }
    }
}
