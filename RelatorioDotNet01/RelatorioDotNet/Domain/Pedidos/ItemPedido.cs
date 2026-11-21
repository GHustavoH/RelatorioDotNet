using HubPedidos.Domain.Comum;

namespace HubPedidos.Domain.Pedidos;

public sealed record ItemPedido
{
    public const int QuantidadeMaxima = 10_000;
    public const decimal PrecoMaximo = 100_000m;
    public const int TamanhoMaximoSku = 40;

    public string Sku { get; }
    public int Quantidade { get; }
    public decimal PrecoUnitario { get; }

    public decimal Subtotal => Quantidade * PrecoUnitario;

    private ItemPedido(string sku, int quantidade, decimal precoUnitario)
    {
        Sku = sku;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    public static Resultado<ItemPedido> Criar(string? sku, int quantidade, decimal precoUnitario)
    {
        var erros = new List<string>();

        var skuLimpo = sku?.Trim();
        if (string.IsNullOrEmpty(skuLimpo))
        {
            erros.Add("sku: obrigatório.");
        }
        else if (skuLimpo.Length > TamanhoMaximoSku)
        {
            erros.Add($"sku: máximo de {TamanhoMaximoSku} caracteres.");
        }

        if (quantidade < 1 || quantidade > QuantidadeMaxima)
        {
            erros.Add($"quantidade: deve estar entre 1 e {QuantidadeMaxima}.");
        }

        if (precoUnitario <= 0 || precoUnitario > PrecoMaximo)
        {
            erros.Add($"precoUnitario: deve ser maior que 0 e no máximo {PrecoMaximo}.");
        }
        else if (decimal.Round(precoUnitario, 2) != precoUnitario)
        {
            erros.Add("precoUnitario: no máximo 2 casas decimais.");
        }

        return erros.Count > 0
            ? Resultado<ItemPedido>.Falha(erros)
            : Resultado<ItemPedido>.Ok(new ItemPedido(skuLimpo!, quantidade, precoUnitario));
    }
}
