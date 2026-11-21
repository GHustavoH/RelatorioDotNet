using System.Threading.Channels;
using Microsoft.AspNetCore.OutputCaching;
using RelatorioFinal.Contratos;
using RelatorioFinal.Dominio;
using RelatorioFinal.Fila;
using RelatorioFinal.Persistencia;

namespace RelatorioFinal.Servicos;

public record ResultadoCriacao(PedidoResponse? Pedido, string? Erro, bool FilaCheia = false);

public interface IPedidoServico
{
    Task<ResultadoCriacao> CriarAsync(CriarPedidoRequest req, CancellationToken ct);
    Task<List<ResultadoCriacao>> CriarLoteAsync(IReadOnlyList<CriarPedidoRequest> requests, CancellationToken ct);
    Task<PedidoEntidade?> ObterAsync(Guid id, CancellationToken ct);
    Task<Pagina<PedidoResumo>> ListarAsync(string? cliente, string? status, string? categoria, int pagina, int tamanho, CancellationToken ct);
}

public class PedidoServico(
    AppDbContext db,
    Channel<ProcessarPedidoJob> canal,
    MetricasFila metricas,
    IOutputCacheStore cache) : IPedidoServico
{
    public async Task<ResultadoCriacao> CriarAsync(CriarPedidoRequest req, CancellationToken ct)
    {
        var resultados = await CriarLoteAsync([req], ct);
        return resultados[0];
    }

    public async Task<List<ResultadoCriacao>> CriarLoteAsync(IReadOnlyList<CriarPedidoRequest> requests, CancellationToken ct)
    {
        var resultados = new ResultadoCriacao?[requests.Count];
        var pendentes = new List<(int Indice, PedidoEntidade Entidade)>();

        for (int i = 0; i < requests.Count; i++)
        {
            var mapeado = PedidoMapeador.ParaDominio(requests[i]);
            if (!mapeado.Ok)
            {
                metricas.Incrementar("invalidos");
                resultados[i] = new(null, mapeado.Erro);
                continue;
            }

            var categoria = Classificador.Classificar(mapeado.Pedido!);
            if (categoria == CategoriaPedido.Invalido)
            {
                metricas.Incrementar("invalidos");
                resultados[i] = new(null, "Pedido invalido");
                continue;
            }

            var entidade = PedidoMapeador.ParaEntidade(mapeado.Pedido!, categoria, requests[i].Cidade);
            db.Pedidos.Add(entidade);
            pendentes.Add((i, entidade));
        }

        // uma unica gravacao para o lote inteiro
        if (pendentes.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var rejeitadas = false;
        foreach (var (indice, entidade) in pendentes)
        {
            // TryWrite nao espera e devolve false quando a fila esta cheia
            if (canal.Writer.TryWrite(new ProcessarPedidoJob(entidade.Id, DateTime.UtcNow)))
            {
                metricas.Incrementar("aceitos");
                resultados[indice] = new(PedidoMapeador.ParaResponse(entidade), null);
            }
            else
            {
                metricas.Incrementar("rejeitados");
                entidade.Status = "Rejeitado";
                entidade.AtualizadoEm = DateTime.UtcNow;
                resultados[indice] = new(null, "Fila cheia, tente novamente mais tarde", true);
                rejeitadas = true;
            }
        }

        if (rejeitadas)
        {
            await db.SaveChangesAsync(ct);
        }

        if (pendentes.Count > 0)
        {
            await cache.EvictByTagAsync("pedidos", ct);
        }

        return resultados.Select(r => r!).ToList();
    }

    public Task<PedidoEntidade?> ObterAsync(Guid id, CancellationToken ct) =>
        ConsultasPedido.BuscarAsync(db, id, ct);

    public Task<Pagina<PedidoResumo>> ListarAsync(string? cliente, string? status, string? categoria, int pagina, int tamanho, CancellationToken ct) =>
        ConsultasPedido.ListarAsync(db, cliente, status, categoria, pagina, tamanho, ct);
}
