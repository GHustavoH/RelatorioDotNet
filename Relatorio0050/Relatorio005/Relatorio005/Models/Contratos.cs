using System.ComponentModel.DataAnnotations;

namespace Relatorio005.Models;

public sealed class CriarMensagemRequest
{
    [Required]
    [StringLength(30, MinimumLength = 2)]
    public string Canal { get; init; } = string.Empty;

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Conteudo { get; init; } = string.Empty;

    [Range(0, 9)]
    public int Prioridade { get; init; }
}

public sealed record RespostaCriacao(Guid Id);
