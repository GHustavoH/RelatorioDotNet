using HubPedidos.Domain.Classificacao;
using HubPedidos.Domain.Pedidos;

namespace HubPedidos.Application.Processamento;

[ProcessadorPedido(CategoriaProcessamento.Padrao, versao: 1)]
public sealed class ProcessadorPadrao : IProcessadorPedido
{
    public ResultadoProcessamento Processar(Pedido pedido) =>
        new("Fluxo padrão: sem ajustes.", pedido.ValorTotal);
}

[ProcessadorPedido(CategoriaProcessamento.Expresso, versao: 1)]
public sealed class ProcessadorExpressoV1 : IProcessadorPedido
{
    public ResultadoProcessamento Processar(Pedido pedido) =>
        new("Expresso v1: taxa de urgência de 5%.", Calculo.Ajustar(pedido.ValorTotal, 0.05m));
}

[ProcessadorPedido(CategoriaProcessamento.Expresso, versao: 2)]
public sealed class ProcessadorExpressoV2 : IProcessadorPedido
{
    public ResultadoProcessamento Processar(Pedido pedido) =>
        new("Expresso v2: taxa de urgência de 4%.", Calculo.Ajustar(pedido.ValorTotal, 0.04m));
}

[ProcessadorPedido(CategoriaProcessamento.Premium, versao: 1)]
public sealed class ProcessadorPremium : IProcessadorPedido
{
    public ResultadoProcessamento Processar(Pedido pedido) =>
        new("Premium: desconto de 3% e conferência manual.", Calculo.Ajustar(pedido.ValorTotal, -0.03m));
}

[ProcessadorPedido(CategoriaProcessamento.Atacado, versao: 1)]
public sealed class ProcessadorAtacado : IProcessadorPedido
{
    public ResultadoProcessamento Processar(Pedido pedido) =>
        new("Atacado: desconto de 8% por volume.", Calculo.Ajustar(pedido.ValorTotal, -0.08m));
}

[ProcessadorPedido(CategoriaProcessamento.Internacional, versao: 1)]
public sealed class ProcessadorInternacional : IProcessadorPedido
{
    private readonly TimeProvider _relogio;

    public ProcessadorInternacional(TimeProvider relogio) => _relogio = relogio;

    public ResultadoProcessamento Processar(Pedido pedido)
    {
        var data = _relogio.GetUtcNow().ToString("yyyy-MM-dd");
        return new($"Internacional: encargos de 12% (calculado em {data} UTC).", Calculo.Ajustar(pedido.ValorTotal, 0.12m));
    }
}
