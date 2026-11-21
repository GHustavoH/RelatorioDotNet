namespace HubPedidos.Application.Processamento;

/// <summary>Falha de configuração do catálogo, com todas as inconsistências listadas na mensagem.</summary>
public sealed class CatalogoProcessadoresException : Exception
{
    public CatalogoProcessadoresException(IReadOnlyList<string> problemas)
        : base(MontarMensagem(problemas))
    {
        Problemas = problemas;
    }

    public IReadOnlyList<string> Problemas { get; }

    private static string MontarMensagem(IReadOnlyList<string> problemas) =>
        "Catálogo de processadores inválido:" + Environment.NewLine
        + string.Join(Environment.NewLine, problemas.Select(p => "- " + p));
}
