namespace HubPedidos.Application.Processamento;

internal static class Calculo
{
    public static decimal Ajustar(decimal valor, decimal percentual) =>
        Math.Round(valor * (1m + percentual), 2, MidpointRounding.ToEven);
}
