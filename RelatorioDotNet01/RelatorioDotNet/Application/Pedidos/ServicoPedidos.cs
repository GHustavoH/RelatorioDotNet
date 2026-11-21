using HubPedidos.Application.Contratos;
using HubPedidos.Application.Mapeamento;
using HubPedidos.Application.Processamento;
using HubPedidos.Domain.Classificacao;
using Microsoft.Extensions.DependencyInjection;

namespace HubPedidos.Application.Pedidos;

public sealed class ServicoPedidos
{
    private readonly CatalogoProcessadores _catalogo;
    private readonly IServiceProvider _servicos;
    private readonly IPedidoRepositorio _repositorio;

    public ServicoPedidos(
        CatalogoProcessadores catalogo,
        IServiceProvider servicos,
        IPedidoRepositorio repositorio)
    {
        _catalogo = catalogo;
        _servicos = servicos;
        _repositorio = repositorio;
    }

    public async Task<ResultadoPedido> ProcessarAsync(CriarPedidoRequest? request, CancellationToken cancellationToken)
    {
        var mapeado = MapeadorPedido.ParaDominio(request);
        if (!mapeado.TryObter(out var pedido))
        {
            return new ResultadoPedido.Invalido(mapeado.Erros);
        }

        var categoria = ClassificadorPedido.Classificar(pedido);
        if (categoria == CategoriaProcessamento.Invalido)
        {
            return new ResultadoPedido.Invalido(new[] { "pedido: não foi possível classificar o pedido." });
        }

        if (!_catalogo.TryObter(categoria, out var descritor))
        {
            // Não deveria ocorrer: o catálogo é validado com cobertura total na inicialização.
            throw new InvalidOperationException($"Nenhum processador registrado para a categoria {categoria}.");
        }

        var processador = (IProcessadorPedido)_servicos.GetRequiredService(descritor.Tipo);
        var resultado = processador.Processar(pedido);

        var resposta = MapeadorPedido.ParaResponse(pedido, categoria, descritor, resultado);

        var salvo = await _repositorio.SalvarAsync(resposta, cancellationToken);
        return salvo
            ? new ResultadoPedido.Criado(resposta)
            : new ResultadoPedido.Conflito($"Já existe um pedido com o id {resposta.Id}.");
    }

    public IReadOnlyList<ProcessadorResponse> ListarProcessadores() =>
        _catalogo.Descritores
            .Select(d => new ProcessadorResponse(d.Categoria.ToString(), d.Versao, d.Nome, _catalogo.EhVigente(d)))
            .ToArray();
}
