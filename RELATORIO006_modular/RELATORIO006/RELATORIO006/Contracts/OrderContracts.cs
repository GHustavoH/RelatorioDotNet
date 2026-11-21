using System.ComponentModel.DataAnnotations;
using Hub.Orders.Domain;

namespace RELATORIO006.Contracts;

// ---------- Comando (não versionado: idêntico em v1 e v2) ----------

public sealed class CreateOrderRequest
{
    [Required, StringLength(120, MinimumLength = 1)]
    public string CustomerName { get; init; } = string.Empty;

    // Literais double: independentes de cultura (a forma com strings quebra em pt-BR).
    [Range(0.01, 1_000_000_000.0)]
    public decimal Amount { get; init; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; init; } = "BRL";
}

public sealed record CreateOrderResponse(Guid Id);

// ---------- v1: contrato CONGELADO. Não alterar (ver docs/api/v1-v2-change-matrix.md) ----------

/// <summary>"total" é um NÚMERO (decimal) na moeda padrão do pedido.</summary>
public sealed record OrderV1Response(
    Guid Id,
    string CustomerName,
    decimal Total,
    string Status,
    DateTimeOffset CreatedAt);

// ---------- v2: mudança INCOMPATÍVEL deliberada ----------

/// <summary>Valor monetário explícito, com moeda. Substitui o número simples de v1.</summary>
public sealed record MoneyV2(decimal Amount, string Currency);

/// <summary>MUDANÇA INCOMPATÍVEL: "total" deixou de ser número e passou a ser objeto { amount, currency }.</summary>
public sealed record OrderV2Response(
    Guid Id,
    string CustomerName,
    MoneyV2 Total,
    string Status,
    DateTimeOffset CreatedAt);

public static class OrderMapping
{
    public static OrderV1Response ToV1(this Order order) => new(
        order.Id,
        order.CustomerName,
        order.Total.Amount,
        order.Status.ToString(),
        order.CreatedAt);

    public static OrderV2Response ToV2(this Order order) => new(
        order.Id,
        order.CustomerName,
        new MoneyV2(order.Total.Amount, order.Total.Currency),
        order.Status.ToString(),
        order.CreatedAt);
}
